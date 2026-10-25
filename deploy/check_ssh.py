"""Read-only verification of the project's configured key-based SSH and SFTP access."""
import json
import sys
import paramiko
from ssh_connection import connect, load_config


def main():
    config = load_config()
    connection = connect()
    try:
        channel = connection.open_session(timeout=config['timeout'])
        try:
            channel.settimeout(config['timeout'])
            channel.exec_command('id -un')
            user = channel.makefile('rb').read(4096).decode().strip()
            if channel.recv_exit_status() != 0 or user != config['username']:
                raise RuntimeError('SSH command did not confirm the expected account')
        finally:
            channel.close()
        sftp = paramiko.SFTPClient.from_transport(connection)
        try:
            current = sftp.readlink('/var/www/ozl/current')
            sftp.stat('/var/www/ozl/releases')
        finally:
            sftp.close()
        print(json.dumps(dict(host=config['host'], port=config['port'], username=user,
                             authentication='publickey', host_fingerprint_verified=True,
                             ssh_command_verified=True, sftp_verified=True,
                             current_site=current, read_only=True)))
    finally:
        connection.close()
    return 0


if __name__ == '__main__':
    try:
        sys.exit(main())
    except Exception as error:
        print('SSH check failed: ' + str(error), file=sys.stderr)
        sys.exit(1)
