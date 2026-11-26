# Security policy

## Supported versions

Only the latest release receives security fixes.

## Reporting a vulnerability

Please do not open a public issue. Report vulnerabilities privately through [GitHub's private vulnerability reporting](https://github.com/akihiro/ComCon/security/advisories/new).

Include what you found, how to reproduce it, and what an attacker could achieve. You should get a reply within a week.

## Scope

ComCon passes the data it receives from the serial device to Windows Terminal unchanged, including escape sequences. That is how it renders colours and cursor movement. Escape sequences that change the window title, the clipboard (OSC 52) or the screen state are therefore expected behaviour of any VT terminal and are not treated as vulnerabilities in ComCon itself. Issues that let a device do more than a normal terminal allows, such as running commands on the local machine, are in scope.
