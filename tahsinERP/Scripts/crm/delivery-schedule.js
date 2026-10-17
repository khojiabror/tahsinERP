/*
 * Buyurtma yetkazib berish rejasi (SOrder/DeliverySchedule) — avval view ichida inline edi (CRM-D20/M03).
 * Sozlamalar <script type="application/json" id="deliveryScheduleConfig"> blokidan olinadi:
 *   changePermit  — SaveDeliverySchedule uchun o'zgartirish huquqi (server bilan bir xil qoida)
 *   orderProducts — [{ SOrderProductID, ProductPNo, ProductName, OrderQuantity, PlannedQuantity }]
 *   rowIndex      — yangi qatorlar uchun boshlang'ich indeks (mavjud ScheduleItems soni)
 */
(function () {
    'use strict';

    function escapeHtml(value) {
        if (value === null || value === undefined) return '';
        return String(value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function readConfig() {
        var el = document.getElementById('deliveryScheduleConfig');
        if (!el) return null;
        try {
            return JSON.parse(el.textContent || el.innerHTML || '{}');
        } catch (e) {
            return null;
        }
    }

    function init() {
        var $ = window.jQuery || window.$j;
        var cfg = readConfig();
        if (!$ || !cfg) return;

        var changePermit = cfg.changePermit === true;
        var orderProducts = cfg.orderProducts || [];
        var scheduleRowIndex = parseInt(cfg.rowIndex, 10) || 0;

        // Huquq bo'lmasa barcha maydonlar o'chiriladi
        if (!changePermit) {
            $('.delivery-date, .qty-planned, .order-product-select').prop('disabled', true);
            $('#addScheduleRow').prop('disabled', true);
        }

        function updateRowNumbers() {
            $('#scheduleTableBody tr').each(function (index) {
                $(this).find('td:first').text(index + 1);
            });
        }

        // Yangi reja qatori
        $('#addScheduleRow').on('click', function () {
            if (!changePermit) {
                return false;
            }
            var prefix = 'ScheduleItems[' + scheduleRowIndex + '].';
            var row = '<tr>' +
                '<td>' + (scheduleRowIndex + 1) + '</td>' +
                '<td>' +
                '<select name="' + prefix + 'OrderProductID" class="form-control order-product-select" required>' +
                '<option value="">---Tanlang---</option>';

            for (var i = 0; i < orderProducts.length; i++) {
                var product = orderProducts[i];
                var remaining = product.OrderQuantity - product.PlannedQuantity;
                if (remaining > 0) {
                    row += '<option value="' + escapeHtml(product.SOrderProductID) + '" data-order-qty="' + escapeHtml(product.OrderQuantity) + '" data-planned="' + escapeHtml(product.PlannedQuantity) + '">' +
                        escapeHtml(product.ProductPNo) + ' - ' + escapeHtml(product.ProductName) + ' (Qolgan: ' + remaining.toFixed(2) + ')' +
                        '</option>';
                }
            }

            row += '</select>' +
                '</td>' +
                '<td>' +
                '<input type="date" name="' + prefix + 'DeliveryDate" class="form-control delivery-date" required />' +
                '</td>' +
                '<td>' +
                '<input type="number" name="' + prefix + 'QtyPlanned" class="form-control qty-planned" value="0" step="0.01" min="0.01" required />' +
                '</td>' +
                '<td>' +
                '<button type="button" class="btn btn-sm btn-danger remove-schedule-row">' +
                '<i class="bx bx-trash"></i>' +
                '</button>' +
                '</td>' +
                '</tr>';

            $('#scheduleTableBody').append(row);
            scheduleRowIndex++;
            updateRowNumbers();
        });

        $(document).on('click', '.remove-schedule-row', function () {
            if (confirm('Ushbu qatorni o\'chirishni tasdiqlaysizmi?')) {
                $(this).closest('tr').remove();
                updateRowNumbers();
            }
        });

        // Maxsulot tanlanganda maksimal miqdorni yangilash
        $(document).on('change', '.order-product-select', function () {
            var $row = $(this).closest('tr');
            var $option = $(this).find('option:selected');
            var orderQty = parseFloat($option.data('order-qty')) || 0;
            var planned = parseFloat($option.data('planned')) || 0;
            var remaining = orderQty - planned;

            var $qtyInput = $row.find('.qty-planned');
            $qtyInput.attr('max', remaining);
            $qtyInput.attr('data-order-qty', orderQty);
            $qtyInput.attr('data-planned', planned);
        });

        $(document).on('input', '.qty-planned', function () {
            var qty = parseFloat($(this).val()) || 0;
            var maxQty = parseFloat($(this).attr('max')) || 0;

            if (qty > maxQty) {
                $(this).addClass('is-invalid');
                alert('Miqdor qolgan miqdordan (' + maxQty.toFixed(2) + ') oshib ketdi!');
                $(this).val(maxQty);
            } else {
                $(this).removeClass('is-invalid');
            }
        });

        $('#scheduleForm').on('submit', function (e) {
            var hasRows = $('#scheduleTableBody tr').length > 0;
            if (!hasRows) {
                e.preventDefault();
                alert('Kamida bitta yetkazib berish rejasi qo\'shish kerak!');
                return false;
            }

            var isValid = true;
            $('#scheduleTableBody tr').each(function () {
                var $qtyInput = $(this).find('.qty-planned');
                var qty = parseFloat($qtyInput.val()) || 0;
                var maxQty = parseFloat($qtyInput.attr('max')) || 0;

                if (qty <= 0 || qty > maxQty) {
                    isValid = false;
                    $qtyInput.addClass('is-invalid');
                } else {
                    $qtyInput.removeClass('is-invalid');
                }
            });

            if (!isValid) {
                e.preventDefault();
                alert('Barcha miqdorlar to\'g\'ri kiritilganligini tekshiring!');
                return false;
            }
        });

        // Bo'sh sana maydoniga bugungi sana
        $(document).on('focus', '.delivery-date', function () {
            if (!$(this).val()) {
                var today = new Date().toISOString().split('T')[0];
                $(this).val(today);
            }
        });
    }

    // jQuery ready — avvalgi inline $(document).ready bilan bir xil tartibda ishlashi uchun
    if (window.jQuery) {
        window.jQuery(init);
    } else if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
