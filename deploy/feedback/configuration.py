"""Insert/remove only this collector's includes in a hash-checked live config."""
import hashlib
import re

HTTP = '    # BEGIN OZL feedback v1 http\n    include /etc/ozl-feedback/nginx-http.conf;\n    # END OZL feedback v1 http\n'
LOCATION = '        # BEGIN OZL feedback v1 location\n        include /etc/ozl-feedback/nginx-location.conf;\n        # END OZL feedback v1 location\n'


def check(text, expected):
    if hashlib.sha256(text.encode('utf-8')).hexdigest() != expected:
        raise ValueError('Concurrent nginx configuration change; no replacement allowed')


def patch_nginx(text, expected):
    check(text, expected)
    if '/api/feedback' in text or '/etc/ozl-feedback/' in text or 'OZL feedback v1' in text:
        raise ValueError('Existing feedback route requires explicit migration')
    openings = list(re.finditer(r'(?m)^http\s*\{\s*$', text))
    if len(openings) != 1:
        raise ValueError('Expected exactly one top-level http block')
    result = text[:openings[0].end()] + '\n' + HTTP + text[openings[0].end():]
    for port in (18080, 18081):
        matches = list(re.finditer(r'(?m)^[ \t]*listen[ \t]+127\.0\.0\.1:' + str(port) + r';[ \t]*$', result))
        if len(matches) != 1:
            raise ValueError('Expected one existing loopback listener ' + str(port))
        end = matches[0].end()
        result = result[:end] + '\n' + LOCATION + result[end:]
    return result


def unpatch_nginx(text, expected):
    check(text, expected)
    if text.count('\n' + HTTP) != 1 or text.count('\n' + LOCATION) != 2:
        raise ValueError('Owned feedback include blocks are missing or modified')
    return text.replace('\n' + HTTP, '', 1).replace('\n' + LOCATION, '')
