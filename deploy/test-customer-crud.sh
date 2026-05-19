#!/bin/bash
BASE="http://localhost:5000"
COOKIE="/tmp/crud-cookies.txt"

# Login
rm -f $COOKIE
CSRF=$(curl -s -c $COOKIE $BASE/api/auth/login | grep -oP 'name="__RequestVerificationToken" type="hidden" value="\K[^"]+' | head -1)
curl -s -b $COOKIE -c $COOKIE -X POST $BASE/api/auth/login \
    -H "Content-Type: application/json" \
    -H "RequestVerificationToken: $CSRF" \
    -d '{"email":"admin@crypto.com","password":"Admin123!","rememberMe":false}' > /dev/null

echo "=== CUSTOMER CREATE ==="
RESULT=$(curl -s -b $COOKIE -X POST $BASE/api/customers/create \
    -H "Content-Type: application/json" \
    -d '{"firstName":"Test","lastName":"Customer","phone":"555-0100","email":"test@test.com"}')
echo "$RESULT"
CUST_ID=$(echo "$RESULT" | python3 -c "import sys,json; print(json.load(sys.stdin).get('id',''))" 2>/dev/null)
echo "Created ID: $CUST_ID"

if [ -n "$CUST_ID" ] && [ "$CUST_ID" != "" ]; then
    echo ""
    echo "=== CUSTOMER READ ==="
    curl -s -b $COOKIE $BASE/api/customers/$CUST_ID
    echo ""
    
    echo "=== CUSTOMER UPDATE ==="
    curl -s -o /dev/null -w "HTTP %{http_code}" -b $COOKIE -X PUT $BASE/api/customers/$CUST_ID \
        -H "Content-Type: application/json" \
        -d '{"firstName":"Updated","lastName":"Customer","phone":"555-0200","email":"updated@test.com"}'
    echo ""
    
    echo "=== CUSTOMER DELETE ==="
    curl -s -o /dev/null -w "HTTP %{http_code}" -b $COOKIE -X DELETE $BASE/api/customers/$CUST_ID
    echo ""
    
    echo "=== VERIFY SOFT DELETE ==="
    sqlite3 /var/www/cryptopayment/invoice.db "SELECT Id, FirstName, IsDeleted FROM Customers WHERE Id=$CUST_ID;"
fi

echo ""
echo "=== CUSTOMER COUNT ==="
curl -s -b $COOKIE $BASE/api/customers/GetTotalCustomerCount
echo ""

echo "=== INVOICE COUNT ==="
curl -s -b $COOKIE $BASE/api/invoices/GetTotalInvoiceCount
echo ""

echo "=== ROLE COUNT ==="
curl -s -b $COOKIE $BASE/api/roles/GetTotalRoleCount
echo ""

rm -f $COOKIE
