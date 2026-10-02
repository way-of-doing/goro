#!/usr/bin/env bash
# redact-session.sh -- scrub exported Claude Code session transcripts before they are committed.
#
# Usage:
#   redact-session.sh [file.md ...]      # default: <script dir>/../docs/sessions/*.md (skips *.redacted.md)
#
# For every input FILE.md this script:
#   1. writes the scrubbed copy to FILE.redacted.md (the original is never modified),
#   2. prints a per-rule count of what it changed (stderr),
#   3. shows `diff -u FILE.md FILE.redacted.md` so you can see exactly what was altered (stdout),
#   4. runs "tripwire" checks on the result and flags lines a human should read (stderr).
#
# Exit status: 0 = ran clean, 2 = tripwires fired (read the REVIEW lines before committing),
#              1 = the script itself failed.
#
# Optional project-specific denylist: a text file with one literal string per line
# (customer names, internal hostnames, ...). Default location: .redact-terms next to this script; override with
# REDACT_TERMS_FILE=path. Lines starting with # are comments. Matching is case-insensitive.
#
# Requires: bash, perl, diff. Pattern-based: it catches KNOWN shapes of leaks. The diff shows what
# it changed, never what it missed -- skim the redacted file before you commit it.

set -euo pipefail

# Locate this script's own directory (following symlinks where `readlink -f` is available), so the
# default works no matter which directory you run it from.
_src="${BASH_SOURCE[0]}"
if _real="$(readlink -f -- "$_src" 2>/dev/null)" && [[ -n "$_real" ]]; then _src="$_real"; fi
SCRIPT_DIR="$(cd -P -- "$(dirname -- "$_src")" && pwd)"

SESSIONS_DIR="${SESSIONS_DIR:-$(dirname "$SCRIPT_DIR")/docs/sessions}"   # i.e. <script dir>/../docs/sessions
TERMS_FILE="${REDACT_TERMS_FILE:-$SCRIPT_DIR/.redact-terms}"

command -v perl >/dev/null || { echo "redact-session.sh: perl is required" >&2; exit 1; }

# ---------------------------------------------------------------------------------------------
# Literal terms to blank out wherever they appear: the person and machine behind the transcript.
# GUARDS AGAINST: your login name, machine name and git author name turning up in prose or in
# quoted command output (paths are handled separately below). Short values (<4 chars) are
# skipped because they would shred ordinary words.
# ---------------------------------------------------------------------------------------------
TERMS=""
add_term() { if [[ ${#1} -ge 4 ]]; then TERMS+="$1"$'\n'; fi; return 0; }
add_term "${USERNAME:-}"
add_term "${USER:-}"
add_term "$(hostname 2>/dev/null || true)"
add_term "$(git config user.name 2>/dev/null || true)"
# GUARDS AGAINST: anything specific to YOUR project that no generic pattern can know about.
if [[ -f "$TERMS_FILE" ]]; then
  while IFS= read -r line || [[ -n "$line" ]]; do
    line="${line%$'\r'}"
    [[ -z "$line" || "$line" == \#* ]] && continue
    TERMS+="$line"$'\n'
  done < "$TERMS_FILE"
fi
export REDACT_TERMS="$TERMS"

# ---------------------------------------------------------------------------------------------
# The redactor itself. Reads a transcript on stdin, writes the scrubbed one on stdout, and prints
# a summary plus any tripwire warnings on stderr. Exits 2 if a tripwire fired.
# ---------------------------------------------------------------------------------------------
IFS= read -r -d '' PERL_PROG <<'PERL' || true
use strict;
use warnings;
binmode STDIN;
binmode STDOUT;

local $/;                        # slurp the whole transcript
my $t = <STDIN> // '';
my %n;                           # rule name -> number of substitutions made

# ===== A. INJECTED NOISE ===================================================================
# GUARDS AGAINST: bookkeeping that Claude Code stores as if you had typed it -- /model and other
# slash-command echoes, "local command" caveats, hook output, <system-reminder> blocks. Besides
# being clutter, these can carry config, file contents or environment details you never meant to
# publish. Only removed from "## User" blocks, and only when the tag starts a line, so prose that
# merely *discusses* these tags (e.g. in backticks) is left alone.
my $noise = join '|', qw(
  local-command-caveat local-command-stdout local-command-stderr
  command-name command-message command-args
  system-reminder user-prompt-submit-hook session-start-hook
  bash-input bash-stdout bash-stderr
);

my ($head, @blocks) = split /^(?=## (?:User|Claude)\r?$)/m, $t;
$head //= '';
if ($head =~ /\A## (?:User|Claude)/) { unshift @blocks, $head; $head = ''; }

my @kept;
for my $b (@blocks) {
    if ($b =~ /\A## User/) {
        my $removed = ($b =~ s{^[ \t]*<($noise)(?=[\s>])[^>]*>.*?</\1>[ \t]*\r?\n?}{}gms) || 0;
        if ($removed) {
            $n{'noise-tags'} += $removed;
            $b =~ s/\n{3,}/\n\n/g;           # tidy the gaps we just opened, in this block only
        }
        (my $body = $b) =~ s/\A## (?:User|Claude)\r?\n//;
        if ($body !~ /\S/) { $n{'empty-blocks-dropped'}++; next; }   # nothing left -> drop heading too
    }
    push @kept, $b;
}
$t = $head . join('', @kept);

# GUARDS AGAINST: memory-feature markup (<cc-memory ...>) leaking into the published text. Only
# the tags go; the sentence inside is ordinary conversation and stays.
$n{'memory-tags'} += ($t =~ s{</?cc-memory\b[^>]*>}{}g) || 0;

# ===== B. SECRETS ==========================================================================
# GUARDS AGAINST: credentials that got pasted into a prompt, or that Claude quoted from a file
# or command output while talking about it. Replacements use [REDACTED:..] rather than <..>
# because Markdown renderers would swallow an unknown <tag> and hide the fact that anything
# was removed.
my @rules = (
  # Whole private-key blocks (multi-line).
  ['private-key-block', qr/-----BEGIN [A-Z ]*PRIVATE KEY-----.*?-----END [A-Z ]*PRIVATE KEY-----/s, '[REDACTED:private-key]'],

  # Vendor-issued tokens with distinctive prefixes. Cheap, precise, almost no false positives.
  ['api-key (sk-)',     qr/\bsk-[A-Za-z0-9_-]{20,}/,                              '[REDACTED:api-key]'],  # Anthropic, OpenAI, ...
  ['github-token',      qr/\b(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{22,})/, '[REDACTED:github-token]'],
  ['gitlab-token',      qr/\bglpat-[A-Za-z0-9_-]{20,}/,                           '[REDACTED:gitlab-token]'],
  ['aws-access-key',    qr/\b(?:AKIA|ASIA)[0-9A-Z]{16}\b/,                        '[REDACTED:aws-key]'],
  ['slack-token',       qr/\bxox[abprs]-[A-Za-z0-9-]{10,}/,                       '[REDACTED:slack-token]'],
  ['google-api-key',    qr/\bAIza[0-9A-Za-z_-]{35}\b/,                            '[REDACTED:google-key]'],
  ['stripe-key',        qr/\b[sr]k_(?:live|test)_[0-9A-Za-z]{16,}/,               '[REDACTED:stripe-key]'],
  ['npm-token',         qr/\bnpm_[A-Za-z0-9]{36}\b/,                              '[REDACTED:npm-token]'],
  ['nuget-api-key',     qr/\boy2[a-z0-9]{43}\b/,                                  '[REDACTED:nuget-key]'],
  ['jwt',               qr/\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}/, '[REDACTED:jwt]'],

  # HTTP auth headers. "Basic" is only matched after "Authorization:" -- on its own it is an
  # ordinary English word.
  ['bearer-token',      qr/\b(Bearer\s+)[A-Za-z0-9._~+\/=-]{16,}/i,               '${1}[REDACTED:auth]'],
  ['basic-auth-header', qr/\b(Authorization\s*:\s*Basic\s+)[A-Za-z0-9+\/=]{8,}/i, '${1}[REDACTED:auth]'],

  # user:password@ embedded in a URL or connection string.
  ['url-credentials',   qr/\b([a-z][a-z0-9+.-]*:\/\/)[^\s\/@:]+:[^\s\/@]+@/i,     '${1}[REDACTED:credentials]@'],

  # key=value assignments whose KEY name says "secret" (env dumps, .env files, appsettings,
  # connection strings, shell exports). The key is kept so the diff stays readable; the value goes.
  ['secret-assignment', qr/\b([A-Za-z0-9_.-]*(?:password|passwd|secret|api[_-]?key|apikey|access[_-]?key|account[_-]?key|shared[_-]?access[_-]?key|private[_-]?key)[A-Za-z0-9_.-]*\s*=\s*["']?)(?!\[REDACTED)[^\s"'`;,&]{4,}/i, '${1}[REDACTED:secret]'],

  # "token" is a normal word in a parser project, so only long values count.
  ['token-assignment',  qr/\b([A-Za-z0-9_.-]*token[A-Za-z0-9_.-]*\s*[=:]\s*["']?)(?!\[REDACTED)[^\s"'`;,&]{20,}/i, '${1}[REDACTED:secret]'],

  # JSON / quoted-YAML:  "client_secret": "abc..."
  ['secret-json',       qr/(["'][A-Za-z0-9_.-]*(?:password|passwd|secret|api[_-]?key|apikey|access[_-]?key|private[_-]?key|token)[A-Za-z0-9_.-]*["']\s*:\s*["'])(?!\[REDACTED)[^"']{6,}(["'])/i, '${1}[REDACTED:secret]${2}'],

  # ALL_CAPS env-style keys written with a colon (YAML, CI configs).
  ['secret-env-yaml',   qr/\b([A-Z][A-Z0-9_]*(?:PASSWORD|SECRET|TOKEN|API_?KEY|ACCESS_?KEY|PRIVATE_?KEY)[A-Z0-9_]*\s*:\s*)(?!\[REDACTED)[^\s"'`;,]{6,}/, '${1}[REDACTED:secret]'],

# ===== C. PERSONAL / NETWORK IDENTIFIERS ===================================================
  # GUARDS AGAINST: your email, or anyone else's, ending up public. The Co-Authored-By address
  # that Claude Code adds to commits is allowed through.
  ['email',             qr/\b(?!noreply\@anthropic\.com\b)[A-Za-z0-9._%+-]+\@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}\b/, '[REDACTED:email]'],

  # GUARDS AGAINST: LAN / server addresses revealing your network. Loopback and 0.0.0.0 are
  # harmless and kept. (Four-part version numbers can false-positive; the diff will show them.)
  ['ipv4',              qr/\b(?!127\.|0\.0\.0\.0)(?:25[0-5]|2[0-4]\d|1?\d?\d)(?:\.(?:25[0-5]|2[0-4]\d|1?\d?\d)){3}\b/, '[REDACTED:ip]'],
);

for my $r (@rules) {
    my ($name, $re, $rep) = @$r;
    my $count = 0;
    # $rep is a template like '${1}[REDACTED:auth]': keep the captured prefix, swap out the secret.
    $t =~ s!$re!
        $count++;
        my @c = (undef, $1, $2, $3);                  # snapshot captures before the nested s///
        (my $out = $rep) =~ s/\$\{(\d)\}/defined $c[$1] ? $c[$1] : ''/ge;
        $out
    !ge;
    $n{$name} += $count;
}

# ===== D. LOCAL PATHS ======================================================================
# GUARDS AGAINST: your OS account name leaking through home-directory paths -- the most common
# accidental leak in agent transcripts (tool errors, "I edited /home/you/...", stack traces).
# The rest of the path is kept so the text stays understandable.
my $home = $ENV{HOME} // '';
$n{'home-dir'} += ($t =~ s/\Q$home\E/~/g) || 0 if length($home) > 1;
# Windows: C:\Users\name, C:/Users/name, doubled backslashes from JSON-escaped text.
$n{'home-dir'} += ($t =~ s{[A-Za-z]:[\\/]{1,2}Users[\\/]{1,2}[^\\/\s"'`<>|:*?)\]]+}{~}g) || 0;
# Git Bash (/c/Users/name) and WSL (/mnt/c/Users/name).
$n{'home-dir'} += ($t =~ s{(?<![\w.~-])(?:/mnt)?/[a-z]/Users/[^/\s"'`<>|:*?)\]]+}{~}g) || 0;
# Linux / macOS. The lookbehind avoids URL paths such as example.com/home/page.
$n{'home-dir'} += ($t =~ s{(?<![\w.~-])/(?:home|Users)/[^/\s"'`<>|:*?)\]]+}{~}g) || 0;

# ===== E. LITERAL TERMS ====================================================================
# GUARDS AGAINST: login name, hostname, git author name and anything from your .redact-terms file.
for my $term (grep { length } split /\n/, ($ENV{REDACT_TERMS} // '')) {
    $n{'literal-terms'} += ($t =~ s/\Q$term\E/[REDACTED]/gi) || 0;
}

# ===== F. TIMESTAMP OFFSETS ================================================================
# GUARDS AGAINST: the UTC offset in "_Exported 2026-09-30T01:30:37+02:00_" -- a small but free
# hint about where you live and when you work. The time itself is kept; only the offset goes.
$n{'tz-offset'} += ($t =~ s/(\d{4}-\d\d-\d\d[T ]\d\d:\d\d:\d\d(?:\.\d+)?)[+-]\d\d:?\d\d\b/$1/g) || 0;

print $t;
print "\n" if length($t) && $t !~ /\n\z/;

# ===== SUMMARY =============================================================================
my @hit = sort grep { $n{$_} } keys %n;
if (@hit) {
    print STDERR "  changes made:\n";
    printf STDERR "    %-24s %d\n", $_, $n{$_} for @hit;
} else {
    print STDERR "  changes made: none\n";
}

# ===== G. TRIPWIRES (report only -- nothing is changed here) ================================
# GUARDS AGAINST: whatever the rules above did not recognise. Line numbers refer to the
# .redacted.md output. The offending text is deliberately NOT echoed, so a secret doesn't get
# copied into your terminal scrollback or CI logs a second time.
my ($warn, $i) = (0, 0);
for my $line (split /\n/, $t, -1) {
    $i++;
    my @why;
    push @why, 'leftover injected-context tag'
        if $line =~ /<\/?(?:system-reminder|local-command-|command-(?:name|message|args)|user-prompt-submit-hook|session-start-hook|cc-memory)/;
    push @why, 'key/certificate marker'
        if $line =~ /-----BEGIN [A-Z ]*(?:PRIVATE KEY|OPENSSH|CERTIFICATE)/;
    push @why, 'absolute path outside ~'
        if $line =~ m{(?:^|[\s"'(`])(?:/home|/Users|/root)/} || $line =~ m{\b[A-Za-z]:\\};
    for my $tok ($line =~ /[A-Za-z0-9_+=-]{32,}/g) {      # long mixed-case+digit blob = maybe a key
        if ($tok =~ /[a-z]/ && $tok =~ /[A-Z]/ && $tok =~ /\d/) { push @why, 'long random-looking token'; last; }
    }
    if (@why) {
        $warn++;
        print STDERR "  REVIEW line $i: ", join('; ', @why), "\n";
    }
}
exit($warn ? 2 : 0);
PERL

# ---------------------------------------------------------------------------------------------
# Driver
# ---------------------------------------------------------------------------------------------
DIFF_ARGS=(-u)
if diff --color=auto /dev/null /dev/null >/dev/null 2>&1; then DIFF_ARGS+=(--color=auto); fi

redact_one() {
  local in="$1" out="${1%.md}.redacted.md" rc=0
  echo "== $in -> $out" >&2
  perl -e "$PERL_PROG" < "$in" > "$out" || rc=$?
  if (( rc != 0 && rc != 2 )); then
    rm -f "$out"
    echo "redact-session.sh: redaction failed for $in (perl exit $rc)" >&2
    return 1
  fi
  echo "--- diff $in $out"
  if diff -q "$in" "$out" >/dev/null; then
    echo "(no differences)"
  else
    diff "${DIFF_ARGS[@]}" "$in" "$out" || true   # diff exits 1 when files differ; that's expected
  fi
  return "$rc"
}

files=("$@")
if (( ${#files[@]} == 0 )); then
  shopt -s nullglob
  files=("$SESSIONS_DIR"/*.md)
  shopt -u nullglob
fi

status=0
processed=0
for f in "${files[@]}"; do
  [[ "$f" == *.redacted.md ]] && continue          # never re-redact our own output
  [[ -f "$f" ]] || { echo "redact-session.sh: not a file: $f" >&2; status=1; continue; }
  processed=$((processed + 1))
  rc=0
  redact_one "$f" || rc=$?
  (( rc > status )) && status=$rc
done

(( processed > 0 )) || echo "redact-session.sh: no session files found (looked in $SESSIONS_DIR)" >&2
exit "$status"
