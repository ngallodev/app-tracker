#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${1:-http://localhost:5000}"
TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

request() {
  local method="$1"
  local path="$2"
  local body="${3:-}"
  local headers_file="$TMP_DIR/headers.txt"
  local body_file="$TMP_DIR/body.txt"
  local status

  if [[ -n "$body" ]]; then
    status="$(curl -sS -X "$method" "$BASE_URL$path" \
      -H "Content-Type: application/json" \
      -D "$headers_file" \
      -o "$body_file" \
      -w "%{http_code}" \
      --data "$body")"
  else
    status="$(curl -sS -X "$method" "$BASE_URL$path" \
      -D "$headers_file" \
      -o "$body_file" \
      -w "%{http_code}")"
  fi

  echo "$status"
}

assert_contains() {
  local file="$1"
  local pattern="$2"
  if ! grep -q "$pattern" "$file"; then
    echo "Assertion failed: '$pattern' not found in $file" >&2
    cat "$file" >&2
    exit 1
  fi
}

echo "Smoke checks against $BASE_URL"

status="$(request GET /healthz)"
[[ "$status" == "200" ]] || { echo "Expected 200 for /healthz, got $status" >&2; exit 1; }
assert_contains "$TMP_DIR/body.txt" '"status":"healthy"'
echo "PASS /healthz"

status="$(request GET /healthz/ready)"
[[ "$status" == "200" || "$status" == "503" ]] || { echo "Expected 200/503 for /healthz/ready, got $status" >&2; exit 1; }
assert_contains "$TMP_DIR/body.txt" '"dependencies"'
assert_contains "$TMP_DIR/body.txt" '"database"'
echo "PASS /healthz/ready"

status="$(request GET /missing-route)"
[[ "$status" == "404" ]] || { echo "Expected 404 for missing route, got $status" >&2; exit 1; }
assert_contains "$TMP_DIR/body.txt" '"title":"Not Found"'
assert_contains "$TMP_DIR/body.txt" '"status":404'
assert_contains "$TMP_DIR/body.txt" '"correlationId"'
echo "PASS 404 problem details shape"

status="$(request POST /api/jobs/ '{}')"
[[ "$status" == "400" ]] || { echo "Expected 400 for invalid job payload, got $status" >&2; exit 1; }
assert_contains "$TMP_DIR/body.txt" '"title"'
assert_contains "$TMP_DIR/body.txt" '"status":400'
echo "PASS 400 problem details shape"

CORRELATION_ID="smoke-test-correlation-id"
status="$(curl -sS -X GET "$BASE_URL/healthz" \
  -H "X-Correlation-ID: $CORRELATION_ID" \
  -D "$TMP_DIR/headers.txt" \
  -o "$TMP_DIR/body.txt" \
  -w "%{http_code}")"
[[ "$status" == "200" ]] || { echo "Expected 200 for correlation-id echo check, got $status" >&2; exit 1; }
assert_contains "$TMP_DIR/headers.txt" "X-Correlation-ID: $CORRELATION_ID"
echo "PASS correlation-id propagation"

INVALID_ANALYSIS_PAYLOAD='{"jobId":"00000000-0000-0000-0000-000000000000","resumeId":"00000000-0000-0000-0000-000000000000"}'
for i in 1 2 3; do
  status="$(request POST /api/analyses/ "$INVALID_ANALYSIS_PAYLOAD")"
done
[[ "$status" == "429" ]] || { echo "Expected 429 after repeated analysis calls, got $status" >&2; exit 1; }
assert_contains "$TMP_DIR/body.txt" '"title":"Too Many Requests"'
assert_contains "$TMP_DIR/body.txt" '"status":429'
assert_contains "$TMP_DIR/body.txt" '"retryAfter"'
echo "PASS rate-limit problem details shape"

echo "All day4 smoke checks passed."
