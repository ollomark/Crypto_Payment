#!/bin/bash
# Register a user on the VDS
set -e

# Step 1: Get the register page and extract CSRF token + cookies
RESPONSE=$(curl -s -c /tmp/cookies.txt http://localhost:5000/api/auth/register)
TOKEN=$(echo "$RESPONSE" | grep -oP 'name="__RequestVerificationToken" type="hidden" value="\K[^"]+' | head -1)

echo "CSRF Token: ${TOKEN:0:20}..."

# Step 2: Register user with CSRF token
RESULT=$(curl -s -b /tmp/cookies.txt -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -H "RequestVerificationToken: $TOKEN" \
  -d '{"email":"admin@crypto.com","password":"Admin123!","fullName":"Admin User","userName":"admin"}')

echo "Register result: $RESULT"

# Step 3: Confirm email directly in SQLite
sqlite3 /var/www/cryptopayment/invoice.db "UPDATE AspNetUsers SET EmailConfirmed = 1 WHERE Email = 'admin@crypto.com';"
echo "Email confirmed in database."

# Verify
sqlite3 /var/www/cryptopayment/invoice.db "SELECT Id, Email, EmailConfirmed, FullName FROM AspNetUsers;"
