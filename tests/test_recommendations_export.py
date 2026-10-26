"""Durable public-feed publishing; all stores and network fixtures are temporary."""
import importlib.util
import json
from pathlib import Path
import tempfile
import threading
import unittest
from unittest import mock


class ExportTests(unittest.TestCase):
    def setUp(self):
        module = Path(__file__).resolve().parents[1] / "deploy/recommendations/server.py"
        spec = importlib.util.spec_from_file_location("recommendation_export_fixture", str(module))
        self.service = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.service)
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        root = Path(self.temporary.name)
        self.private, self.public = root / "private", root / "public"
        self.public.mkdir()
        self.feed = self.public / "feed.json"
        self.service.setup_private(self.private)
        self.app = self.open()

    def open(self):
        app = self.service.Application(self.private, export_feed=self.feed)
        self.addCleanup(app.close)
        return app

    def add(self, rows):
        rows.append({"id": "a" * 32, "name": "fixture", "host": "127.0.0.1", "port": 16261,
                     "queryPort": 0, "version": "42", "description": "plain", "cover": "",
                     "tags": [], "order": 0, "useSteamRelay": False, "published": True})

    def feed_document(self):
        return json.loads(self.feed.read_text(encoding="utf-8"))

    def test_export_is_exact_public_projection_and_restart_is_idempotent(self):
        self.app.mutate(0, self.add)
        public = self.feed_document()
        self.assertEqual({"schema", "revision", "updatedUtc", "servers"}, set(public))
        self.assertEqual(1, public["revision"])
        self.assertNotIn("published", public["servers"][0])
        self.assertNotIn("password", self.feed.read_text(encoding="utf-8"))
        before = self.feed.read_bytes()
        self.app.close()
        self.app = self.open()
        self.assertEqual(before, self.feed.read_bytes())
        self.assertFalse(self.app.publication()["pending"])

    def test_export_failure_preserves_old_file_and_restart_flushes_committed_revision(self):
        before = self.feed.read_bytes()
        with mock.patch.object(self.service.os, "replace", side_effect=OSError("fixture export failure")):
            with self.assertRaises(self.service.PublishPending) as error:
                self.app.mutate(0, self.add)
        self.assertEqual(1, error.exception.document["revision"])
        self.assertTrue(error.exception.document["publication"]["pending"])
        self.assertEqual(before, self.feed.read_bytes())
        self.assertEqual(1, self.app.document()["revision"])
        self.app.close()
        self.app = self.open()
        self.assertEqual(1, self.feed_document()["revision"])
        self.assertFalse(self.app.publication()["pending"])

    def test_crash_after_rename_before_ack_is_reconciled_without_revision_increment(self):
        acknowledge = self.app.acknowledge
        self.app.acknowledge = lambda *_: (_ for _ in ()).throw(OSError("fixture lost ack"))
        with self.assertRaises(self.service.PublishPending):
            self.app.mutate(0, self.add)
        self.assertEqual(1, self.feed_document()["revision"])
        self.app.acknowledge = acknowledge
        self.app.close()
        self.app = self.open()
        self.assertEqual(1, self.app.document()["revision"])
        self.assertFalse(self.app.publication()["pending"])

    def test_same_revision_concurrency_has_one_winner_and_latest_export(self):
        results = []
        def write():
            try:
                results.append(self.app.mutate(0, self.add)["revision"])
            except self.service.Rejected as error:
                results.append(error.status)
        threads = [threading.Thread(target=write) for _ in range(2)]
        for worker in threads:
            worker.start()
        for worker in threads:
            worker.join(5)
            self.assertFalse(worker.is_alive())
        self.assertCountEqual([1, 409], results)
        self.assertEqual(1, self.feed_document()["revision"])

    def test_existing_foreign_export_is_never_adopted_and_singleton_is_enforced(self):
        with self.assertRaises((OSError, RuntimeError)):
            self.service.Application(self.private, export_feed=self.feed)
        self.app.close()
        other = self.public / "foreign.json"
        other.write_text("foreign", encoding="utf-8")
        with self.assertRaises((OSError, ValueError)):
            self.service.Application(self.private, export_feed=other)
        self.assertEqual("foreign", other.read_text(encoding="utf-8"))

    def test_foreign_edit_refuses_export_and_keeps_pending_snapshot(self):
        self.feed.write_text("foreign edit", encoding="utf-8")
        with self.assertRaises(self.service.PublishPending):
            self.app.mutate(0, self.add)
        self.assertEqual("foreign edit", self.feed.read_text(encoding="utf-8"))
        self.assertTrue(self.app.publication()["pending"])

    def test_bound_publisher_refuses_a_nonexport_writer_and_a_preexisting_legacy_writer(self):
        with self.assertRaises(ValueError):
            self.service.Application(self.private)
        legacy_private = self.private.parent / "legacy-private"
        self.service.setup_private(legacy_private)
        legacy = self.service.Application(legacy_private)
        self.addCleanup(legacy.close)
        published = self.service.Application(legacy_private, export_feed=self.public / "legacy-feed.json")
        self.addCleanup(published.close)
        with self.assertRaises(self.service.Rejected):
            legacy.mutate(0, self.add)
        self.assertEqual(0, published.document()["revision"])
        self.assertFalse(published.publication()["pending"])

    def test_closed_publisher_cannot_keep_writing_after_releasing_the_singleton(self):
        old = self.app
        old.close()
        self.app = self.open()
        with self.assertRaises(self.service.Rejected):
            old.mutate(0, self.add)
        self.assertEqual(0, self.app.document()["revision"])


if __name__ == "__main__":
    unittest.main()
