/*
 * Yo'l varaqasi formasi (Create/Edit) — avval ikkala view'da ~110 qatorli nusxa edi (LOG-D02).
 * Sozlamalar #waybillForm data-atributlaridan olinadi:
 *   data-tariffs-url, data-invoice-url, data-search-url, data-selected-tariff, data-edit ("true"/"false").
 * Invoys/buyurtma/TTNK tanlovlari select2 AJAX orqali (LOG-P01: avval barcha yozuvlar <option> sifatida yuklanardi).
 */
(function ($) {
    'use strict';

    $(function () {
        var form = $('#waybillForm');
        if (!form.length) return;

        var tariffsUrl = form.data('tariffs-url');
        var invoiceUrl = form.data('invoice-url');
        var searchUrl = form.data('search-url');
        var selectedTariffId = String(form.data('selected-tariff') || '');
        var isEdit = String(form.data('edit')) === 'true';
        var selectText = window.erpT ? erpT('Js_Select') : 'Tanlang';

        // --- hujjat tanlovlari (AJAX)
        $('.waybill-doc-select').each(function () {
            var el = $(this);
            if (el.hasClass('select2-hidden-accessible')) el.select2('destroy');
            el.select2({
                width: '100%',
                allowClear: true,
                placeholder: selectText,
                minimumInputLength: 1,
                ajax: {
                    url: searchUrl,
                    dataType: 'json',
                    delay: 250,
                    data: function (params) { return { type: el.data('doc-type'), q: params.term }; },
                    processResults: function (data) { return { results: data.results || [] }; }
                }
            });
        });

        // --- tariflar
        function loadTariffs(keepSelection) {
            var contractId = $('#contractSelect').val();
            var tariffSelect = $('#tariffSelect');
            if (!contractId) {
                tariffSelect.empty().append($('<option>').val('').text(selectText)).trigger('change.select2');
                return;
            }

            $.post(tariffsUrl, { contractId: contractId }).done(function (result) {
                if (!result || !result.success) return;

                tariffSelect.empty().append($('<option>').val('').text(selectText));
                $.each(result.items, function (_, item) {
                    tariffSelect.append($('<option>')
                        .val(item.id)
                        .text(item.text)
                        .attr('data-rate', item.rate)
                        .attr('data-basis', item.basis)
                        .attr('data-currency', item.currencyId)
                        .attr('data-transport', item.transportTypeId)
                        .attr('data-mincharge', item.minimumCharge));
                });

                if (keepSelection && selectedTariffId) {
                    // Edit: saqlangan stavka tarif bilan qayta yozilmasin (LOG-B05) — faqat tanlov tiklanadi
                    tariffSelect.val(selectedTariffId).trigger('change.select2');
                } else {
                    tariffSelect.trigger('change');
                }
            });
        }

        function applyTariff() {
            var option = $('#tariffSelect option:selected');
            if (!option.val()) return;

            $('#rateInput').val(option.data('rate'));
            $('#freightBasisSelect').val(option.data('basis'));
            if (option.data('currency')) $('#currencySelect').val(option.data('currency')).trigger('change.select2');
            if (option.data('transport')) $('#transportTypeSelect').val(option.data('transport')).trigger('change.select2');
            updateAmountFromTariff();
        }

        // Server bilan bir xil qoida: Services/FreightCalculator (MinimumCharge bilan, LOG-B06)
        function updateAmountFromTariff() {
            var basis = parseInt($('#freightBasisSelect').val(), 10);
            var rate = parseFloat($('#rateInput').val());
            var grWeight = parseFloat($('#GrWeight').val());
            var distance = parseFloat($('#DistanceKm').val());
            var minCharge = parseFloat($('#tariffSelect option:selected').data('mincharge'));
            if (isNaN(rate) || isNaN(basis)) return;

            var amount = rate;
            if (basis === 1) amount = rate * (isNaN(grWeight) ? 0 : grWeight);
            else if (basis === 2) amount = rate * (isNaN(distance) ? 0 : distance);
            if (!isNaN(minCharge) && amount < minCharge) amount = minCharge;

            $('#amountInput').val(amount.toFixed(2)).data('auto', true);
        }

        function requestInvoiceData(invoiceId, invoiceType) {
            if (!invoiceId) return;
            $.post(invoiceUrl, { invoiceId: invoiceId, invoiceType: invoiceType }).done(function (result) {
                if (!result || !result.success) return;
                if (result.fromLocation) $('#FromLocation').val(result.fromLocation);
                if (result.toLocation) $('#ToLocation').val(result.toLocation);
                if (result.direction !== undefined) $('#Direction').val(result.direction);
                if (result.currencyId) $('#currencySelect').val(result.currencyId).trigger('change.select2');
                if (result.payerType !== undefined) $('#PayerType').val(result.payerType);
            });
        }

        $('#contractSelect').on('change', function () { loadTariffs(false); });
        $('#tariffSelect').on('change', applyTariff);
        $('#rateInput, #GrWeight, #DistanceKm, #freightBasisSelect').on('change', function () {
            if ($('#amountInput').data('auto') === true) updateAmountFromTariff();
        });
        $('#amountInput').on('input', function () { $(this).data('auto', false); });
        $('#purchaseInvoiceSelect').on('change', function () { requestInvoiceData($(this).val(), 'Purchase'); });
        $('#salesInvoiceSelect').on('change', function () { requestInvoiceData($(this).val(), 'Sales'); });

        if (isEdit) loadTariffs(true);
    });
})(jQuery);
