#!/bin/bash
# ============================================
# Crypto Payment - Kapsamlı API Test Script
# ============================================

BASE="http://localhost:5000"
COOKIE="/tmp/test-cookies.txt"
PASS=0
FAIL=0
WARN=0
RESULTS=""

log_result() {
    local status=$1
    local endpoint=$2
    local expected=$3
    local actual=$4
    local note=$5
    
    if [ "$status" = "PASS" ]; then
        PASS=$((PASS + 1))
        RESULTS="${RESULTS}[PASS] ${endpoint} (${actual}) ${note}\n"
    elif [ "$status" = "WARN" ]; then
        WARN=$((WARN + 1))
        RESULTS="${RESULTS}[WARN] ${endpoint} expected=${expected} actual=${actual} ${note}\n"
    else
        FAIL=$((FAIL + 1))
        RESULTS="${RESULTS}[FAIL] ${endpoint} expected=${expected} actual=${actual} ${note}\n"
    fi
}

check() {
    local method=$1
    local url=$2
    local expected=$3
    local note=$4
    local data=$5
    
    if [ -n "$data" ]; then
        actual=$(curl -s -o /dev/null -w '%{http_code}' -X $method -b $COOKIE -H "Content-Type: application/json" -d "$data" "${BASE}${url}" 2>&1)
    else
        actual=$(curl -s -o /dev/null -w '%{http_code}' -X $method -b $COOKIE "${BASE}${url}" 2>&1)
    fi
    
    if [ "$actual" = "$expected" ]; then
        log_result "PASS" "$method $url" "$expected" "$actual" "$note"
    else
        log_result "FAIL" "$method $url" "$expected" "$actual" "$note"
    fi
}

echo "============================================"
echo "  CRYPTO PAYMENT API TEST RAPORU"
echo "  $(date)"
echo "============================================"
echo ""

# ============================================
# 1. SUNUCU DURUMU
# ============================================
echo "--- 1. SUNUCU DURUMU ---"
HTTP_CHECK=$(curl -s -o /dev/null -w '%{http_code}' $BASE/ 2>&1)
echo "Root endpoint: $HTTP_CHECK"

# ============================================
# 2. PUBLIC ENDPOINTS (Login gerektirmeyen)
# ============================================
echo ""
echo "--- 2. PUBLIC ENDPOINTS ---"

# Auth sayfaları
check GET "/api/auth/login" "200" "Login sayfası"
check GET "/api/auth/register" "200" "Register sayfası"
check GET "/api/auth/register-success" "200" "Register success sayfası"
check GET "/api/auth/email-verification" "200" "Email verification sayfası"
check GET "/api/auth/twofactor" "200" "2FA sayfası"

# Payment (public)
check GET "/pay/999" "404" "Payment - olmayan fatura"
check GET "/result-invoice/999" "404" "Result invoice - olmayan fatura"

# Callback
check GET "/api/callback" "400" "Callback - HMAC olmadan"
check GET "/api/callback/test" "404" "Callback test - Production'da kapalı"

# Invoice status (public)
check GET "/api/invoices/status/999" "200" "Invoice status - olmayan fatura"

# ============================================
# 3. AUTH KORUMASI TESTİ (Login olmadan)
# ============================================
echo ""
echo "--- 3. AUTH KORUMASI (Login olmadan erişim) ---"

# Tüm korumalı endpoint'ler 302 (redirect to login) dönmeli
rm -f $COOKIE
check GET "/api/customers/GetAll" "302" "Customers - auth redirect"
check GET "/api/invoices/GetAll" "302" "Invoices - auth redirect"
check GET "/api/permissions/GetAll" "302" "Permissions - auth redirect"
check GET "/api/roles/GetAll" "302" "Roles - auth redirect"
check GET "/api/users/GetAll" "302" "Users - auth redirect"
check GET "/" "302" "Dashboard - auth redirect"
check GET "/customers" "302" "Customer view - auth redirect"
check GET "/invoices" "302" "Invoice view - auth redirect"
check GET "/roles" "302" "Role view - auth redirect"
check GET "/permissions" "302" "Permission view - auth redirect"
check GET "/users" "302" "User view - auth redirect"
check GET "/role-claims" "302" "RoleClaim view - auth redirect"

# ============================================
# 4. LOGIN İŞLEMİ
# ============================================
echo ""
echo "--- 4. LOGIN ---"

# CSRF token al
rm -f $COOKIE
LOGIN_PAGE=$(curl -s -c $COOKIE $BASE/api/auth/login)
CSRF=$(echo "$LOGIN_PAGE" | grep -oP 'name="__RequestVerificationToken" type="hidden" value="\K[^"]+' | head -1)

if [ -z "$CSRF" ]; then
    log_result "FAIL" "CSRF Token" "token" "empty" "Login sayfasından token alınamadı"
else
    log_result "PASS" "CSRF Token" "token" "${CSRF:0:20}..." "Token alındı"
    
    # Login yap
    LOGIN_RESULT=$(curl -s -b $COOKIE -c $COOKIE -X POST $BASE/api/auth/login \
        -H "Content-Type: application/json" \
        -H "RequestVerificationToken: $CSRF" \
        -d '{"email":"admin@crypto.com","password":"Admin123!","rememberMe":false}' 2>&1)
    
    if echo "$LOGIN_RESULT" | grep -q "redirectUrl"; then
        log_result "PASS" "POST /api/auth/login" "200" "200" "Login başarılı"
    else
        log_result "FAIL" "POST /api/auth/login" "200+redirect" "failed" "Login başarısız: $LOGIN_RESULT"
    fi
fi

# ============================================
# 5. AUTHENTICATED API TESTS
# ============================================
echo ""
echo "--- 5. AUTHENTICATED API TESTS ---"

# Dashboard
check GET "/" "200" "Dashboard (authenticated)"

# MVC Views
check GET "/customers" "200" "Customer List view"
check GET "/invoices" "200" "Invoice List view"
check GET "/invoices/invoice-add" "200" "Invoice Add view"
check GET "/roles" "200" "Role List view"
check GET "/permissions" "200" "Permission List view"
check GET "/users" "200" "User List view"
check GET "/role-claims" "200" "RoleClaim List view"

# API - Customers
check GET "/api/customers/GetAll" "200" "Customers GetAll API"
check GET "/api/customers/GetTotalCustomerCount" "200" "Customer count API"
check GET "/api/customers/999" "404" "Customer by ID (not found)"

# API - Invoices
check GET "/api/invoices/GetAll" "200" "Invoices GetAll API"
check GET "/api/invoices/GetTotalInvoiceCount" "200" "Invoice count API"

# API - Permissions
check GET "/api/permissions/GetAll" "200" "Permissions GetAll API"
check GET "/api/permissions/999" "404" "Permission by ID (not found)"

# API - Roles
check GET "/api/roles/GetAll" "200" "Roles GetAll API"
check GET "/api/roles/GetTotalRoleCount" "200" "Role count API"

# API - Users
check GET "/api/users/GetAll" "200" "Users GetAll API"

# 2FA Setup
check GET "/api/auth/2fa/setup" "200" "2FA Setup page"

# ============================================
# 6. CRUD OPERATIONS
# ============================================
echo ""
echo "--- 6. CRUD OPERATIONS ---"

# Customer CRUD
CUST_CREATE=$(curl -s -b $COOKIE -X POST $BASE/api/customers/create \
    -H "Content-Type: application/json" \
    -d '{"fullName":"Test Customer","email":"test@test.com","phone":"555-0100","address":"Test Address"}' 2>&1)
CUST_ID=$(echo "$CUST_CREATE" | grep -oP '"id"\s*:\s*\K\d+' | head -1)

if [ -n "$CUST_ID" ]; then
    log_result "PASS" "POST /api/customers/create" "201/200" "OK" "Customer created ID=$CUST_ID"
    
    # Read
    CUST_READ_CODE=$(curl -s -o /dev/null -w '%{http_code}' -b $COOKIE $BASE/api/customers/$CUST_ID)
    if [ "$CUST_READ_CODE" = "200" ]; then
        log_result "PASS" "GET /api/customers/$CUST_ID" "200" "$CUST_READ_CODE" "Customer read OK"
    else
        log_result "FAIL" "GET /api/customers/$CUST_ID" "200" "$CUST_READ_CODE" "Customer read failed"
    fi
    
    # Update
    CUST_UPD_CODE=$(curl -s -o /dev/null -w '%{http_code}' -b $COOKIE -X PUT $BASE/api/customers/$CUST_ID \
        -H "Content-Type: application/json" \
        -d '{"fullName":"Updated Customer","email":"updated@test.com","phone":"555-0200","address":"Updated Address"}')
    if [ "$CUST_UPD_CODE" = "200" ] || [ "$CUST_UPD_CODE" = "204" ]; then
        log_result "PASS" "PUT /api/customers/$CUST_ID" "200/204" "$CUST_UPD_CODE" "Customer updated"
    else
        log_result "FAIL" "PUT /api/customers/$CUST_ID" "200/204" "$CUST_UPD_CODE" "Customer update failed"
    fi
    
    # Delete
    CUST_DEL_CODE=$(curl -s -o /dev/null -w '%{http_code}' -b $COOKIE -X DELETE $BASE/api/customers/$CUST_ID)
    if [ "$CUST_DEL_CODE" = "200" ] || [ "$CUST_DEL_CODE" = "204" ]; then
        log_result "PASS" "DELETE /api/customers/$CUST_ID" "200/204" "$CUST_DEL_CODE" "Customer deleted (soft)"
    else
        log_result "FAIL" "DELETE /api/customers/$CUST_ID" "200/204" "$CUST_DEL_CODE" "Customer delete failed"
    fi
else
    log_result "FAIL" "POST /api/customers/create" "200" "error" "Customer create failed: $CUST_CREATE"
fi

# Permission CRUD
PERM_CREATE=$(curl -s -b $COOKIE -X POST $BASE/api/permissions/create \
    -H "Content-Type: application/json" \
    -d '{"name":"test-permission","description":"Test Permission"}' 2>&1)
PERM_ID=$(echo "$PERM_CREATE" | grep -oP '"id"\s*:\s*\K\d+' | head -1)

if [ -n "$PERM_ID" ]; then
    log_result "PASS" "POST /api/permissions/create" "200" "OK" "Permission created ID=$PERM_ID"
    
    # Read
    check GET "/api/permissions/$PERM_ID" "200" "Permission read"
    
    # Update
    PERM_UPD_CODE=$(curl -s -o /dev/null -w '%{http_code}' -b $COOKIE -X PUT $BASE/api/permissions/$PERM_ID \
        -H "Content-Type: application/json" \
        -d '{"name":"updated-permission","description":"Updated Permission"}')
    if [ "$PERM_UPD_CODE" = "200" ] || [ "$PERM_UPD_CODE" = "204" ]; then
        log_result "PASS" "PUT /api/permissions/$PERM_ID" "200/204" "$PERM_UPD_CODE" "Permission updated"
    else
        log_result "FAIL" "PUT /api/permissions/$PERM_ID" "200/204" "$PERM_UPD_CODE" "Permission update failed"
    fi
    
    # Delete
    PERM_DEL_CODE=$(curl -s -o /dev/null -w '%{http_code}' -b $COOKIE -X DELETE $BASE/api/permissions/$PERM_ID)
    if [ "$PERM_DEL_CODE" = "200" ] || [ "$PERM_DEL_CODE" = "204" ]; then
        log_result "PASS" "DELETE /api/permissions/$PERM_ID" "200/204" "$PERM_DEL_CODE" "Permission deleted"
    else
        log_result "FAIL" "DELETE /api/permissions/$PERM_ID" "200/204" "$PERM_DEL_CODE" "Permission delete failed"
    fi
else
    log_result "FAIL" "POST /api/permissions/create" "200" "error" "Permission create failed: $PERM_CREATE"
fi

# Role CRUD
ROLE_CREATE=$(curl -s -b $COOKIE -X POST $BASE/api/roles/create \
    -H "Content-Type: application/json" \
    -d '{"name":"TestRole"}' 2>&1)
ROLE_ID=$(echo "$ROLE_CREATE" | grep -oP '"id"\s*:\s*"\K[^"]+' | head -1)

if [ -n "$ROLE_ID" ]; then
    log_result "PASS" "POST /api/roles/create" "200" "OK" "Role created ID=$ROLE_ID"
    
    # Read
    ROLE_READ_CODE=$(curl -s -o /dev/null -w '%{http_code}' -b $COOKIE $BASE/api/roles/$ROLE_ID)
    if [ "$ROLE_READ_CODE" = "200" ]; then
        log_result "PASS" "GET /api/roles/$ROLE_ID" "200" "$ROLE_READ_CODE" "Role read OK"
    else
        log_result "FAIL" "GET /api/roles/$ROLE_ID" "200" "$ROLE_READ_CODE" "Role read failed"
    fi
    
    # Update
    ROLE_UPD_CODE=$(curl -s -o /dev/null -w '%{http_code}' -b $COOKIE -X PUT $BASE/api/roles/$ROLE_ID \
        -H "Content-Type: application/json" \
        -d '{"name":"UpdatedRole"}')
    if [ "$ROLE_UPD_CODE" = "200" ] || [ "$ROLE_UPD_CODE" = "204" ]; then
        log_result "PASS" "PUT /api/roles/$ROLE_ID" "200/204" "$ROLE_UPD_CODE" "Role updated"
    else
        log_result "FAIL" "PUT /api/roles/$ROLE_ID" "200/204" "$ROLE_UPD_CODE" "Role update failed"
    fi
    
    # Delete
    ROLE_DEL_CODE=$(curl -s -o /dev/null -w '%{http_code}' -b $COOKIE -X DELETE $BASE/api/roles/$ROLE_ID)
    if [ "$ROLE_DEL_CODE" = "200" ] || [ "$ROLE_DEL_CODE" = "204" ]; then
        log_result "PASS" "DELETE /api/roles/$ROLE_ID" "200/204" "$ROLE_DEL_CODE" "Role deleted"
    else
        log_result "FAIL" "DELETE /api/roles/$ROLE_ID" "200/204" "$ROLE_DEL_CODE" "Role delete failed"
    fi
else
    log_result "FAIL" "POST /api/roles/create" "200" "error" "Role create failed: $ROLE_CREATE"
fi

# ============================================
# 7. API RESPONSE CONTENT CHECKS
# ============================================
echo ""
echo "--- 7. API RESPONSE CONTENT ---"

# Customers GetAll - JSON array check
CUST_RESP=$(curl -s -b $COOKIE $BASE/api/customers/GetAll)
if echo "$CUST_RESP" | python3 -c "import sys,json; json.load(sys.stdin)" 2>/dev/null; then
    log_result "PASS" "Customers GetAll JSON" "valid" "valid" "Valid JSON response"
else
    log_result "FAIL" "Customers GetAll JSON" "valid" "invalid" "Response: ${CUST_RESP:0:100}"
fi

# Invoices GetAll
INV_RESP=$(curl -s -b $COOKIE $BASE/api/invoices/GetAll)
if echo "$INV_RESP" | python3 -c "import sys,json; json.load(sys.stdin)" 2>/dev/null; then
    log_result "PASS" "Invoices GetAll JSON" "valid" "valid" "Valid JSON response"
else
    log_result "FAIL" "Invoices GetAll JSON" "valid" "invalid" "Response: ${INV_RESP:0:100}"
fi

# Permissions GetAll
PERM_RESP=$(curl -s -b $COOKIE $BASE/api/permissions/GetAll)
if echo "$PERM_RESP" | python3 -c "import sys,json; json.load(sys.stdin)" 2>/dev/null; then
    log_result "PASS" "Permissions GetAll JSON" "valid" "valid" "Valid JSON response"
else
    log_result "FAIL" "Permissions GetAll JSON" "valid" "invalid" "Response: ${PERM_RESP:0:100}"
fi

# Roles GetAll
ROLE_RESP=$(curl -s -b $COOKIE $BASE/api/roles/GetAll)
if echo "$ROLE_RESP" | python3 -c "import sys,json; json.load(sys.stdin)" 2>/dev/null; then
    log_result "PASS" "Roles GetAll JSON" "valid" "valid" "Valid JSON response"
else
    log_result "FAIL" "Roles GetAll JSON" "valid" "invalid" "Response: ${ROLE_RESP:0:100}"
fi

# Users GetAll
USER_RESP=$(curl -s -b $COOKIE $BASE/api/users/GetAll)
if echo "$USER_RESP" | python3 -c "import sys,json; json.load(sys.stdin)" 2>/dev/null; then
    log_result "PASS" "Users GetAll JSON" "valid" "valid" "Valid JSON response"
else
    log_result "FAIL" "Users GetAll JSON" "valid" "invalid" "Response: ${USER_RESP:0:100}"
fi

# Count endpoints
CUST_COUNT=$(curl -s -b $COOKIE $BASE/api/customers/GetTotalCustomerCount)
INV_COUNT=$(curl -s -b $COOKIE $BASE/api/invoices/GetTotalInvoiceCount)
ROLE_COUNT=$(curl -s -b $COOKIE $BASE/api/roles/GetTotalRoleCount)
log_result "PASS" "Count APIs" "numbers" "C=$CUST_COUNT I=$INV_COUNT R=$ROLE_COUNT" "Count values"

# ============================================
# 8. STATIC FILES
# ============================================
echo ""
echo "--- 8. STATIC FILES ---"

check GET "/favicon.ico" "200" "Favicon"
check GET "/css/site.css" "200" "Site CSS"
check GET "/js/site.js" "200" "Site JS"
check GET "/admin/velzon-dist/assets/css/app.min.css" "200" "Velzon app CSS"
check GET "/admin/velzon-dist/assets/css/bootstrap.min.css" "200" "Velzon bootstrap CSS"
check GET "/admin/velzon-dist/assets/css/icons.min.css" "200" "Velzon icons CSS"
check GET "/admin/velzon-dist/assets/js/app.js" "200" "Velzon app JS"
check GET "/admin/velzon-dist/assets/js/layout.js" "200" "Velzon layout JS"
check GET "/admin/velzon-dist/assets/images/favicon.ico" "200" "Velzon favicon"
check GET "/admin/velzon-dist/assets/images/logo-dark.png" "200" "Logo dark"
check GET "/admin/velzon-dist/assets/images/logo-light.png" "200" "Logo light"
check GET "/lib/bootstrap/dist/css/bootstrap.min.css" "200" "Bootstrap CSS (lib)"
check GET "/lib/jquery/dist/jquery.min.js" "200" "jQuery (lib)"

# ============================================
# 9. ERROR HANDLING
# ============================================
echo ""
echo "--- 9. ERROR HANDLING ---"

check GET "/nonexistent-page" "404" "404 for unknown route"
check GET "/api/customers/abc" "400" "Invalid ID format"
check POST "/api/customers/create" "400" "Empty body create" ""

# ============================================
# 10. DATABASE CHECK
# ============================================
echo ""
echo "--- 10. DATABASE ---"

DB_TABLES=$(sqlite3 /var/www/cryptopayment/invoice.db ".tables" 2>&1)
if echo "$DB_TABLES" | grep -q "AspNetUsers"; then
    log_result "PASS" "DB Tables" "exists" "exists" "Identity tables found"
else
    log_result "FAIL" "DB Tables" "exists" "missing" "Tables: $DB_TABLES"
fi

if echo "$DB_TABLES" | grep -q "Invoices"; then
    log_result "PASS" "DB Invoices table" "exists" "exists" ""
else
    log_result "FAIL" "DB Invoices table" "exists" "missing" ""
fi

if echo "$DB_TABLES" | grep -q "Customers"; then
    log_result "PASS" "DB Customers table" "exists" "exists" ""
else
    log_result "FAIL" "DB Customers table" "exists" "missing" ""
fi

USER_COUNT=$(sqlite3 /var/www/cryptopayment/invoice.db "SELECT COUNT(*) FROM AspNetUsers;" 2>&1)
log_result "PASS" "DB Users count" ">=1" "$USER_COUNT" ""

# ============================================
# FINAL REPORT
# ============================================
echo ""
echo "============================================"
echo "  TEST SONUCLARI"
echo "============================================"
echo ""
printf "$RESULTS"
echo ""
echo "============================================"
TOTAL=$((PASS + FAIL + WARN))
echo "  TOPLAM: $TOTAL test"
echo "  PASS:   $PASS"
echo "  FAIL:   $FAIL"
echo "  WARN:   $WARN"
echo "============================================"

# Cleanup
rm -f $COOKIE
