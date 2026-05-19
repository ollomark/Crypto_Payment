#!/bin/bash
set -e

# Login
RESPONSE=$(curl -s -c /tmp/cookies.txt http://localhost:5000/api/auth/login)
TOKEN=$(echo "$RESPONSE" | grep -oP 'name="__RequestVerificationToken" type="hidden" value="\K[^"]+' | head -1)
curl -s -b /tmp/cookies.txt -c /tmp/cookies.txt -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -H "RequestVerificationToken: $TOKEN" \
  -d '{"email":"admin@crypto.com","password":"Admin123!","rememberMe":true}' > /dev/null
echo "Logged in."

# Create invoice - same as UI would send
echo ""
echo "=== Creating Invoice via API (same as UI) ==="
RESULT=$(curl -s -w "\nHTTP_CODE:%{http_code}" -b /tmp/cookies.txt -X POST http://localhost:5000/api/invoices/invoice-add \
  -H "Content-Type: application/json" \
  -d '{"customerId":1,"currency":"USDT_TRX","sourceCurrency":"USD","sourceAmount":10.00,"orderNumber":"ORD-1707742800","orderName":"testi","email":"test@test.com","callbackUrl":"https://localhost:5001/api/callback","invoiceItemsDto":[{"serviceName":"test2","serviceDescription":"test22","price":10.00,"quantity":1,"total":"$10.00"}]}')
echo "Result: $RESULT"
