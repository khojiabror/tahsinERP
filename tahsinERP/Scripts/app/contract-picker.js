/*
 * Ta'minotchi tanlanganda uning shartnomalarini select'ga yuklaydi (SRM-D13).
 * Avval POrders/Edit, PInvoices/Create va PInvoices/Edit'da uch xil nusxada edi.
 *
 * Server javobi: { success: true, data: [{ ID, ContractNo, VatRate }] } — option'ga data-vat-rate yoziladi.
 * CSRF header'ni Scripts/csrf.js qo'shadi.
 *
 * ContractPicker.load($select, url, supplierId, selectedId, options)
 *   options.disableWithoutSupplier — ta'minotchi tanlanmagan bo'lsa select o'chirilgan qoladi
 *   options.alertOnError           — yuklash xatosida foydalanuvchiga xabar
 */
(function () {
    'use strict';

    var PLACEHOLDER = '<option value="">---Tanlang---</option>';

    function refreshSelect2($select) {
        if (typeof jQuery.fn.select2 !== 'undefined') {
            $select.select2();
        }
    }

    function load($select, url, supplierId, selectedId, options) {
        options = options || {};
        $select.empty().append(PLACEHOLDER);

        if (!supplierId) {
            $select.prop('disabled', !!options.disableWithoutSupplier);
            refreshSelect2($select);
            return;
        }

        $select.prop('disabled', true);
        jQuery.ajax({
            url: url,
            type: 'POST',
            data: { supplierId: supplierId },
            success: function (response) {
                var ok = response && response.success;
                if (ok && Array.isArray(response.data)) {
                    response.data.forEach(function (contract) {
                        var $option = jQuery('<option>')
                            .attr('value', contract.ID)
                            .attr('data-vat-rate', contract.VatRate != null ? contract.VatRate : 0)
                            .text(contract.ContractNo);
                        if (selectedId && String(contract.ID) === String(selectedId)) {
                            $option.attr('selected', 'selected');
                        }
                        $select.append($option);
                    });
                }

                $select.prop('disabled', false);
                refreshSelect2($select);
                if (ok) {
                    $select.trigger('change');
                }
            },
            error: function (xhr, status, error) {
                console.error('Contract loading error:', status, error);
                if (options.alertOnError) {
                    var message = (xhr.responseJSON && xhr.responseJSON.message) || 'Shartnomalarni yuklashda xatolik yuz berdi';
                    alert(message);
                }
                $select.prop('disabled', false);
                refreshSelect2($select);
            }
        });
    }

    window.ContractPicker = { load: load };
})();
