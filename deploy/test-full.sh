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

# Create customer
echo ""
echo "=== Creating Customer ==="
CUST=$(curl -s -b /tmp/cookies.txt -X POST http://localhost:5000/api/customers/create \
  -H "Content-Type: application/json" \
  -d '{"firstName":"Test","lastName":"Customer","email":"test@test.com","companyName":"TestCo","phone":"555-1234"}')
echo "Customer: $CUST"

# Get customers
CUSTS=$(curl -s -b /tmp/cookies.txt http://localhost:5000/api/customers/GetAll)
echo "Customers: $CUSTS"

# Create invoice with 10 USD USDT_TRX (above minimum)
echo ""
echo "=== Creating Invoice (10 USD USDT_TRX) ==="
INV=$(curl -s -b /tmp/cookies.txt -X POST http://localhost:5000/api/invoices/invoice-add \
  -H "Content-Type: application/json" \
  -d '{"customerId":1,"currency":"USDT_TRX","sourceCurrency":"USD","sourceAmount":10.00,"orderNumber":"ORD-TEST-002","orderName":"Test Order","email":"test@test.com","callbackUrl":"http://185.7.243.141:5000/api/callback","invoiceItemsDto":[{"serviceName":"Test Item","serviceDescription":"Test","price":10.00,"quantity":1,"total":"10.00"}]}')
echo "Invoice: $INV"

# Check pay page
echo ""
echo "=== Pay Page ==="
PAY_CODE=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/pay/1)
echo "Pay/1: HTTP $PAY_CODE"

# Check invoice list page
echo ""
echo "=== Invoice List ==="
INVLIST=$(curl -s -b /tmp/cookies.txt http://localhost:5000/api/invoices/GetAll)
echo "Invoices: $INVLIST"
