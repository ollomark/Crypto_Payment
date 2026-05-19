#!/bin/bash
set -e

# Login test
RESPONSE=$(curl -s -c /tmp/cookies.txt http://localhost:5000/api/auth/login)
TOKEN=$(echo "$RESPONSE" | grep -oP 'name="__RequestVerificationToken" type="hidden" value="\K[^"]+' | head -1)
echo "CSRF Token: ${TOKEN:0:30}..."

RESULT=$(curl -s -b /tmp/cookies.txt -c /tmp/cookies.txt -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -H "RequestVerificationToken: $TOKEN" \
  -d '{"email":"admin@crypto.com","password":"Admin123!","rememberMe":true}')
echo "Login: $RESULT"

# Test authenticated endpoints
echo ""
echo "=== Testing Endpoints ==="

# Customers
CUST=$(curl -s -b /tmp/cookies.txt http://localhost:5000/api/customers/GetAll)
echo "Customers: $CUST"

# Invoices
INV=$(curl -s -b /tmp/cookies.txt http://localhost:5000/api/invoices/GetAll)
echo "Invoices: $INV"

# Home page
HOME=$(curl -s -o /dev/null -w "%{http_code}" -b /tmp/cookies.txt http://localhost:5000/)
echo "Home: HTTP $HOME"

# Payment page (public)
PAY=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/pay/1)
echo "Pay/1: HTTP $PAY"

# Invoice add page
INVADD=$(curl -s -o /dev/null -w "%{http_code}" -b /tmp/cookies.txt http://localhost:5000/api/invoice/invoice-add)
echo "Invoice Add Page: HTTP $INVADD"
