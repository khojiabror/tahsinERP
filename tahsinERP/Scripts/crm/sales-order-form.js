/*
 * Savdo buyurtmasi formasi (SOrder Create/Edit) — avval ikkala view'da alohida nusxa edi (CRM-D20/M03).
 * Sozlamalar <script type="application/json" id="salesOrderFormConfig"> blokidan olinadi:
 *   contractsUrl, productsUrl     — GetContractsByCustomer / GetContractProducts manzillari
 *   showStatus                    — #productsContainer/#noProductsMessage bilan holat xabarlari (Create);
 *                                   false bo'lsa jadval shunchaki tozalanadi (Edit)
 *   handleErrors                  — AJAX xatolarida xabar ko'rsatish (Create)
 *   resetContractsOnEmptyCustomer — xaridor tanlanmasa shartnomalar ro'yxatini tozalash (Create)
 *   emptyContractsOption          — shartnoma topilmasa "Shartnomalar topilmadi" varianti (Create)
 *   loadContractsOnInit           — sahifa ochilganda tanlangan xaridor shartnomalarini yuklash (Create)
 *   recalcOnInit                  — sahifa ochilganda jami summani qayta hisoblash (Edit)
 *   requireQuantity               — submit oldidan kamida bitta miqdor tekshiruvi (Create)
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
        var el = document.getElementById('salesOrderFormConfig');
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

        var hasSelect2 = !!($.fn && $.fn.select2);
        var $customer = $('#CustomerID');
        var $contract = $('#ContractID');

        if (hasSelect2) {
            $('#CustomerID, #ContractID').select2({ width: '100%' });
        }

        function refreshSelect2($select) {
            if (hasSelect2) {
                $select.select2('destroy').select2({ width: '100%' });
            }
        }

        function option(value, text) {
            return $('<option></option>').val(value).text(text);
        }

        function showMessage(html) {
            $('#productsContainer').hide();
            $('#noProductsMessage').html(html).show();
        }

        function clearProductsTable() {
            $('#productsTableBody').empty();
            calculateTotal();
        }

        // Mahsulotlar yo'q holati: Create'da xabar, Edit'da jadval tozalanadi
        function resetProducts(html) {
            if (cfg.showStatus) {
                showMessage(html);
            } else {
                clearProductsTable();
            }
        }

        function calculateTotal() {
            var total = 0;
            $('.total-price').each(function () {
                total += parseFloat($(this).text()) || 0;
            });
            $('#grandTotal').text(total.toFixed(2));
        }

        // Qatorlarni qayta indekslash (ProductQuantities[i] nomlari va tartib raqami)
        function reindexProductRows() {
            $('#productsTableBody tr').each(function (rowIndex) {
                $(this).find('td:first').text(rowIndex + 1);
                $(this).find('input[name^="ProductQuantities["]').each(function () {
                    var name = $(this).attr('name');
                    if (!name) {
                        return;
                    }
                    var updated = name.replace(/ProductQuantities\[\d+\]\./, 'ProductQuantities[' + rowIndex + '].');
                    $(this).attr('name', updated);
                });
            });
        }

        /**
         * keepContractId — sahifa xato bilan qaytganda server tanlagan shartnoma: ro'yxatda bo'lsa
         * tanlangan holda qoladi va kiritilgan miqdorlar tozalanmaydi (CRM-B11).
         */
        function loadContracts(customerId, keepContractId) {
            $contract.prop('disabled', true).empty().append(option('0', 'Yuklanmoqda...'));

            var request = {
                url: cfg.contractsUrl,
                type: 'GET',
                data: { customerId: customerId },
                dataType: 'json',
                success: function (data) {
                    $contract.empty().append(option('0', '---Tanlang---'));

                    if (data && data.length > 0) {
                        $.each(data, function (i, contract) {
                            $contract.append(option(contract.ID, contract.ContractNo));
                        });
                    } else if (cfg.emptyContractsOption) {
                        $contract.append(option('0', 'Shartnomalar topilmadi'));
                    }

                    var keep = keepContractId && keepContractId != '0' &&
                        $contract.find('option').filter(function () { return this.value == keepContractId; }).length > 0;
                    if (keep) {
                        $contract.val(String(keepContractId));
                    }

                    refreshSelect2($contract);
                    $contract.prop('disabled', false);

                    if (keep) {
                        // Server qayta chizgan qatorlar (#productsContainer boshida yashirin)
                        if ($('#productsTableBody tr').length > 0) {
                            if (cfg.showStatus) {
                                $('#noProductsMessage').hide();
                                $('#productsContainer').show();
                            }
                            $('.order-quantity').trigger('input');
                        }
                        return;
                    }
                    resetProducts('<i class="bx bx-info-circle"></i> Shartnoma tanlang, maxsulotlar yuklanadi.');
                }
            };
            if (cfg.handleErrors) {
                request.error = function () {
                    $contract.empty().append(option('0', 'Xatolik yuz berdi'));
                    $contract.prop('disabled', false);
                    alert('Shartnomalarni yuklashda xatolik yuz berdi.');
                };
            }
            $.ajax(request);
        }

        function buildProductRow(index, product) {
            var price = product.PiecePrice || 0;
            var vatRate = product.VatRate || 0;
            var prefix = 'ProductQuantities[' + index + '].';
            return '<tr>' +
                '<td>' + (index + 1) + '</td>' +
                '<td>' + escapeHtml(product.ProductPNo) + '</td>' +
                '<td>' + escapeHtml(product.ProductName) + '</td>' +
                '<td>' + escapeHtml(product.UnitName) + '</td>' +
                '<td class="text-right">' + parseFloat(product.ContractAmount || 0).toFixed(2) + '</td>' +
                '<td class="text-right">' + (parseFloat(price) * (1 + parseFloat(vatRate) / 100)).toFixed(2) + '</td>' +
                '<td>' +
                '<input type="hidden" name="' + prefix + 'ContractProductID" value="' + escapeHtml(product.ContractProductID) + '" />' +
                '<input type="hidden" name="' + prefix + 'ProductID" value="' + escapeHtml(product.ProductID) + '" />' +
                '<input type="hidden" name="' + prefix + 'PiecePrice" value="' + escapeHtml(price) + '" />' +
                '<input type="hidden" name="' + prefix + 'UnitID" value="' + escapeHtml(product.UnitID) + '" />' +
                '<input type="hidden" name="' + prefix + 'VatRate" value="' + escapeHtml(vatRate) + '" />' +
                '<input type="number" name="' + prefix + 'OrderQuantity" class="form-control order-quantity" value="0" step="0.01" min="0.01" data-price="' + escapeHtml(price) + '" data-vat-rate="' + escapeHtml(vatRate) + '" required />' +
                '</td>' +
                '<td class="text-right total-price">0.00</td>' +
                '<td class="text-center">' +
                '<button type="button" class="btn btn-sm btn-outline-danger remove-product" title="O\'chirish">' +
                '<i class="bx bx-trash"></i>' +
                '</button>' +
                '</td>' +
                '</tr>';
        }

        function loadProducts(contractId) {
            if (cfg.showStatus) {
                showMessage('<i class="bx bx-loader bx-spin"></i> Maxsulotlar yuklanmoqda...');
            }

            var request = {
                url: cfg.productsUrl,
                type: 'GET',
                data: { contractId: contractId },
                dataType: 'json',
                success: function (data) {
                    if (data && data.length > 0) {
                        var tbody = $('#productsTableBody');
                        tbody.empty();
                        $.each(data, function (index, product) {
                            tbody.append(buildProductRow(index, product));
                        });

                        if (cfg.showStatus) {
                            $('#productsContainer').show();
                            $('#noProductsMessage').hide();
                        }
                        calculateTotal();
                    } else {
                        resetProducts('<i class="bx bx-info-circle"></i> Bu shartnomada maxsulotlar topilmadi.');
                    }
                }
            };
            if (cfg.handleErrors) {
                request.error = function () {
                    showMessage('<i class="bx bx-error"></i> Maxsulotlarni yuklashda xatolik yuz berdi.');
                };
            }
            $.ajax(request);
        }

        // Xaridor o'zgarganda shartnomalarni yuklash
        $customer.on('change', function () {
            var customerId = $(this).val();
            if (customerId && customerId != '0') {
                loadContracts(customerId);
                return;
            }
            if (cfg.resetContractsOnEmptyCustomer) {
                $contract.empty().append(option('0', '---Tanlang---'));
                refreshSelect2($contract);
            }
            resetProducts('<i class="bx bx-info-circle"></i> Xaridor tanlang, keyin shartnoma tanlang.');
        });

        // Shartnoma o'zgarganda mahsulotlarni yuklash
        $contract.on('change', function () {
            var contractId = $(this).val();
            if (contractId && contractId != '0') {
                loadProducts(contractId);
            } else {
                resetProducts('<i class="bx bx-info-circle"></i> Shartnoma tanlang, maxsulotlar yuklanadi.');
            }
        });

        // Miqdor o'zgarganda qator va jami summani yangilash
        $(document).on('input', '.order-quantity', function () {
            var $row = $(this).closest('tr');
            var qty = parseFloat($(this).val()) || 0;
            var price = parseFloat($(this).data('price')) || 0;
            var vatRate = parseFloat($(this).data('vat-rate')) || 0;
            var lineBase = qty * price;
            var lineTotal = lineBase + (lineBase * vatRate / 100);
            $row.find('.total-price').text(lineTotal.toFixed(2));
            calculateTotal();
        });

        // Qatorni o'chirish: qolgan qatorlar qayta indekslanadi
        $(document).on('click', '.remove-product', function () {
            $(this).closest('tr').remove();
            reindexProductRows();
            calculateTotal();
        });

        if (cfg.requireQuantity) {
            $('#orderForm').on('submit', function (e) {
                var hasQty = false;
                $('.order-quantity').each(function () {
                    if (parseFloat($(this).val()) > 0) {
                        hasQty = true;
                        return false;
                    }
                });

                if (!hasQty) {
                    e.preventDefault();
                    alert('Kamida bitta maxsulot uchun miqdor kiritish kerak!');
                    return false;
                }
            });
        }

        if (cfg.loadContractsOnInit) {
            var initialCustomerId = $customer.val();
            if (initialCustomerId && initialCustomerId != '0') {
                loadContracts(initialCustomerId, $contract.val());
            }
        }

        // Edit: mavjud maxsulotlar serverda render qilingan — qayta yuklanmaydi. Qator summalari
        // miqdor x narx'dan qayta hisoblanadi: server "N2" bilan uz-UZ formatida ("37 440 000,00")
        // yozgani uchun matnni parseFloat qilish 37 berardi.
        if (cfg.recalcOnInit) {
            $('.order-quantity').trigger('input');
            calculateTotal();
        }
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
