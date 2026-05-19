#!/bin/bash
set -e

# Login first
RESPONSE=$(curl -s -c /tmp/cookies.txt http://localhost:5000/api/auth/login)
TOKEN=$(echo "$RESPONSE" | grep -oP 'name="__RequestVerificationToken" type="hidden" value="\K[^"]+' | head -1)

curl -s -b /tmp/cookies.txt -c /tmp/cookies.txt -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -H "RequestVerificationToken: $TOKEN" \
  -d '{"email":"admin@crypto.com","password":"Admin123!","rememberMe":true}' > /dev/null

echo "Logged in."

# Create a customer
echo ""
echo "=== Creating Customer ==="
CUST=$(curl -s -b /tmp/cookies.txt -X POST http://localhost:5000/api/customers/customer-add \
  -H "Content-Type: application/json" \
  -d '{"firstName":"Test","lastName":"Customer","email":"test@test.com","companyName":"TestCo","phone":"555-1234"}')
echo "Customer: $CUST"

# Get customers
echo ""
echo "=== Getting Customers ==="
CUSTS=$(curl -s -b /tmp/cookies.txt http://localhost:5000/api/customers/GetAll)
echo "Customers: $CUSTS"

# Test invoice add page
echo ""
echo "=== Invoice Add Page ==="
INVADD=$(curl -s -o /dev/null -w "%{http_code}" -b /tmp/cookies.txt http://localhost:5000/invoices/invoice-add)
echo "Invoice Add Page: HTTP $INVADD"

# Create invoice with USDT_TRX
echo ""
echo "=== Creating Invoice (USDT_TRX) ==="
INV=$(curl -s -b /tmp/cookies.txt -X POST http://localhost:5000/api/invoices/invoice-add \
  -H "Content-Type: application/json" \
  -d '{"customerId":1,"currency":"USDT_TRX","sourceCurrency":"USD","sourceAmount":1.00,"orderNumber":"ORD-TEST-001","orderName":"Test Order","email":"test@test.com","callbackUrl":"http://185.7.243.141:5000/api/callback","invoiceItemsDto":[{"serviceName":"Test Item","serviceDescription":"Test","price":1.00,"quantity":1,"total":"1.00"}]}')
echo "Invoice Result: $INV"

# Check pay page
echo ""
echo "=== Pay Page ==="
PAY=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/pay/1)
echo "Pay/1: HTTP $PAY"
