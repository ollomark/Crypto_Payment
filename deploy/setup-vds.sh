#!/bin/bash
# ============================================
# Crypto Payment - Ubuntu VDS Kurulum Scripti
# Düşük kaynaklı sunucu için optimize edilmiş
# ============================================
set -e

echo "=========================================="
echo "  Crypto Payment VDS Kurulumu Başlıyor"
echo "=========================================="

# 1. Sistem güncelleme
echo "[1/7] Sistem güncelleniyor..."
sudo apt update && sudo apt upgrade -y

# 2. .NET 8 Runtime kurulumu (SDK değil, sadece runtime - daha hafif)
echo "[2/7] .NET 8 ASP.NET Runtime kuruluyor..."
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
sudo apt update
sudo apt install -y aspnetcore-runtime-8.0

# 3. PostgreSQL kurulumu
echo "[3/7] PostgreSQL kuruluyor..."
sudo apt install -y postgresql postgresql-contrib

# 4. PostgreSQL yapılandırma
echo "[4/7] PostgreSQL yapılandırılıyor..."
sudo -u postgres psql -c "CREATE USER cryptouser WITH PASSWORD 'BURAYA_GUCLU_SIFRE_YAZ';"
sudo -u postgres psql -c "CREATE DATABASE cryptopayment OWNER cryptouser;"
sudo -u postgres psql -c "GRANT ALL PRIVILEGES ON DATABASE cryptopayment TO cryptouser;"

# 5. Nginx kurulumu (reverse proxy)
echo "[5/7] Nginx kuruluyor..."
sudo apt install -y nginx

# 6. Uygulama klasörü oluştur
echo "[6/7] Uygulama klasörü oluşturuluyor..."
sudo mkdir -p /var/www/cryptopayment
sudo chown $USER:$USER /var/www/cryptopayment

# 7. Systemd servisi oluştur
echo "[7/7] Systemd servisi oluşturuluyor..."
sudo tee /etc/systemd/system/cryptopayment.service > /dev/null <<EOF
[Unit]
Description=Crypto Payment Web App
After=network.target postgresql.service

[Service]
WorkingDirectory=/var/www/cryptopayment
ExecStart=/usr/bin/dotnet /var/www/cryptopayment/Crypto_Payment.dll
Restart=always
RestartSec=10
SyslogIdentifier=cryptopayment
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5000
Environment=DATABASE_URL=postgres://cryptouser:BURAYA_GUCLU_SIFRE_YAZ@localhost:5432/cryptopayment
Environment=Plisio__ApiKey=BURAYA_PLISIO_API_KEY

[Install]
WantedBy=multi-user.target
EOF

echo ""
echo "=========================================="
echo "  Temel kurulum tamamlandı!"
echo "=========================================="
echo ""
echo "SONRAKİ ADIMLAR:"
echo "1. /etc/systemd/system/cryptopayment.service dosyasındaki şifreleri değiştir"
echo "2. Windows'tan publish edip dosyaları VDS'e kopyala"
echo "3. Nginx yapılandırmasını kur (nginx-config.sh)"
echo "4. Servisi başlat"
