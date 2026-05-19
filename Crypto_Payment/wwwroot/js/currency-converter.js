/**
 * Canlı Çoklu Para Birimi Dönüştürücü
 * Varsayılan: EUR (€)
 * Desteklenen: EUR, USD, TRY
 * Kurları exchangerate-api.com'dan çeker, fallback olarak hardcoded kurlar kullanır.
 * localStorage ile seçimi hatırlar.
 */
(function () {
    'use strict';

    const STORAGE_KEY = 'selectedCurrency';
    const RATES_CACHE_KEY = 'currencyRates';
    const RATES_CACHE_TTL = 600000; // 10 dakika

    const CURRENCIES = {
        EUR: { symbol: '€', locale: 'de-DE' },
        USD: { symbol: '$', locale: 'en-US' },
        TRY: { symbol: '₺', locale: 'tr-TR' }
    };

    // Fallback kurlar (EUR bazlı) — API çalışmazsa kullanılır
    const FALLBACK_RATES = { EUR: 1, USD: 1.08, TRY: 38.5 };

    let currentRates = { ...FALLBACK_RATES };
    let currentCurrency = localStorage.getItem(STORAGE_KEY) || 'EUR';

    // Geçerli para birimi mi kontrol et
    if (!CURRENCIES[currentCurrency]) currentCurrency = 'EUR';

    /**
     * API'den güncel kurları çek (EUR bazlı)
     */
    async function fetchRates() {
        // Önce cache kontrol
        try {
            const cached = JSON.parse(localStorage.getItem(RATES_CACHE_KEY));
            if (cached && cached.rates && (Date.now() - cached.ts) < RATES_CACHE_TTL) {
                currentRates = cached.rates;
                return;
            }
        } catch (_) { }

        try {
            const resp = await fetch('https://api.exchangerate-api.com/v4/latest/EUR');
            if (!resp.ok) throw new Error('API error');
            const data = await resp.json();
            currentRates = {
                EUR: 1,
                USD: data.rates.USD || FALLBACK_RATES.USD,
                TRY: data.rates.TRY || FALLBACK_RATES.TRY
            };
            localStorage.setItem(RATES_CACHE_KEY, JSON.stringify({ rates: currentRates, ts: Date.now() }));
        } catch (e) {
            console.warn('[CurrencyConverter] API hatası, fallback kurlar kullanılıyor:', e.message);
            currentRates = { ...FALLBACK_RATES };
        }
    }

    /**
     * Tutarı formatla
     */
    function formatAmount(amount, currencyCode) {
        const info = CURRENCIES[currencyCode] || CURRENCIES.EUR;
        try {
            return info.symbol + ' ' + amount.toLocaleString('tr-TR', {
                minimumFractionDigits: 2,
                maximumFractionDigits: 2
            });
        } catch (_) {
            return info.symbol + ' ' + formatMoney(amount);
        }
    }

    /**
     * Kaynak para biriminden hedef para birimine çevir
     * Önce EUR'a normalize et, sonra hedef'e çevir
     */
    function convertFromSource(amount, sourceCurrency) {
        const srcCode = mapCurrencyCode(sourceCurrency);
        const srcRate = currentRates[srcCode] || 1;
        const eurAmount = amount / srcRate; // EUR'a normalize
        const tgtRate = currentRates[currentCurrency] || 1;
        return eurAmount * tgtRate;
    }

    /**
     * Para birimi kodunu normalize et (EURO->EUR, TL->TRY)
     */
    function mapCurrencyCode(code) {
        if (!code) return 'EUR';
        const c = code.toUpperCase();
        if (c === 'EURO') return 'EUR';
        if (c === 'TL') return 'TRY';
        return c;
    }

    /**
     * Sayfadaki tüm .currency-display elementlerini güncelle
     * data-source-currency varsa kaynak para biriminden çevirir
     * yoksa EUR bazlı kabul eder
     */
    function convertAll() {
        document.querySelectorAll('.currency-display').forEach(el => {
            const baseAmount = parseFloat(el.getAttribute('data-base-amount'));
            if (isNaN(baseAmount)) return;
            const srcCurrency = el.getAttribute('data-source-currency') || 'EUR';
            const converted = convertFromSource(baseAmount, srcCurrency);
            el.textContent = formatAmount(converted, currentCurrency);
        });
    }

    /**
     * Para birimini değiştir
     */
    function switchCurrency(code) {
        if (!CURRENCIES[code]) return;
        currentCurrency = code;
        localStorage.setItem(STORAGE_KEY, code);
        convertAll();
        updateSwitcherUI();
        // Custom event — chart'lar vs. dinleyebilir
        document.dispatchEvent(new CustomEvent('currencyChanged', {
            detail: { currency: code, symbol: CURRENCIES[code].symbol, rate: currentRates[code] || 1 }
        }));
    }

    /**
     * Switcher butonlarının aktif durumunu güncelle
     */
    function updateSwitcherUI() {
        document.querySelectorAll('.currency-btn').forEach(btn => {
            const code = btn.getAttribute('data-currency');
            if (code === currentCurrency) {
                btn.classList.add('active');
            } else {
                btn.classList.remove('active');
            }
        });
    }

    /**
     * Başlat
     */
    async function init() {
        await fetchRates();
        convertAll();
        updateSwitcherUI();

        // Switcher butonlarına event listener ekle
        document.querySelectorAll('.currency-btn').forEach(btn => {
            btn.addEventListener('click', function (e) {
                e.preventDefault();
                const code = this.getAttribute('data-currency');
                switchCurrency(code);
            });
        });
    }

    /**
     * Sayıyı Türk formatında göster: 20965.00 → 20.965,00
     */
    function formatMoney(amount) {
        if (amount == null || isNaN(amount)) return '0,00';
        return Number(amount).toLocaleString('tr-TR', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });
    }

    /**
     * Fiat para birimi kodundan sembol döndür
     */
    function currencySymbol(code) {
        if (!code) return '€';
        const symbols = {
            'USD': '$', 'EUR': '€', 'EURO': '€', 'TRY': '₺', 'TL': '₺',
            'GBP': '£', 'JPY': '¥', 'CNY': '¥', 'KRW': '₩', 'RUB': '₽',
            'INR': '₹', 'BRL': 'R$', 'AUD': 'A$', 'CAD': 'C$', 'CHF': 'CHF'
        };
        return symbols[code.toUpperCase()] || code;
    }

    // Global API — HEMEN tanımla (diğer scriptler beklemeden kullanabilsin)
    window.CurrencyConverter = {
        switchCurrency: switchCurrency,
        convertAll: convertAll,
        getCurrentCurrency: function () { return currentCurrency; },
        getCurrentSymbol: function () { return (CURRENCIES[currentCurrency] || CURRENCIES.EUR).symbol; },
        getCurrentRate: function () { return currentRates[currentCurrency] || 1; },
        formatAmount: formatAmount,
        formatMoney: formatMoney,
        currencySymbol: currencySymbol,
        getRates: function () { return { ...currentRates }; },
        convert: function (eurAmount, targetCurrency) {
            const rate = currentRates[targetCurrency || currentCurrency] || 1;
            return eurAmount * rate;
        },
        convertFromSource: convertFromSource,
        mapCurrencyCode: mapCurrencyCode,
        ready: false
    };

    // Kısa yollar — global scope'a da ekle
    window.formatMoney = formatMoney;
    window.currencySymbol = currencySymbol;

    // DOM hazır olduğunda başlat
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', async function () {
            await init();
            window.CurrencyConverter.ready = true;
            document.dispatchEvent(new CustomEvent('currencyReady', {
                detail: { currency: currentCurrency, symbol: CURRENCIES[currentCurrency].symbol, rate: currentRates[currentCurrency] || 1 }
            }));
        });
    } else {
        init().then(function () {
            window.CurrencyConverter.ready = true;
            document.dispatchEvent(new CustomEvent('currencyReady', {
                detail: { currency: currentCurrency, symbol: CURRENCIES[currentCurrency].symbol, rate: currentRates[currentCurrency] || 1 }
            }));
        });
    }
})();
