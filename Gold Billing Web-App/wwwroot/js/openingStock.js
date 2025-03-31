$(document).ready(function () {
    // Calculate derived fields for a row
    function calculateRow(row) {
        const weight = parseFloat(row.find('.weight-input').val()) || 0;
        const less = parseFloat(row.find('.less-input').val()) || 0;
        const tunch = parseFloat(row.find('.tunch-input').val()) || 0;
        const wastage = parseFloat(row.find('.wastage-input').val()) || 0;
        const rate = parseFloat(row.find('.rate-input').val()) || 0;

        console.log(`Calculating row - Weight: ${weight}, Less: ${less}, Tunch: ${tunch}, Wastage: ${wastage}, Rate: ${rate}`);

        const netWt = weight - less;
        const tw = tunch + wastage;
        const fine = netWt * (tw / 100);
        const amount = fine * rate;

        row.find('.netwt-input').val(netWt.toFixed(2));
        row.find('.tw-input').val(tw.toFixed(2));
        row.find('.fine-input').val(fine.toFixed(2));
        row.find('.amount-input').val(amount.toFixed(2));
        row.find('.hidden-amount-input').val(amount.toFixed(2));

        updateTotals();
    }

    // Update totals in the footer
    function updateTotals() {
        console.log("Updating totals...");
        let totalGrWt = 0, totalLess = 0, totalNetWt = 0, totalFine = 0, totalAmount = 0;

        $('#openingStockTable tbody tr').each(function () {
            totalGrWt += parseFloat($(this).find('.weight-input').val()) || 0;
            totalLess += parseFloat($(this).find('.less-input').val()) || 0;
            totalNetWt += parseFloat($(this).find('.netwt-input').val()) || 0;
            totalFine += parseFloat($(this).find('.fine-input').val()) || 0;
            totalAmount += parseFloat($(this).find('.amount-input').val()) || 0;
        });

        $('#totalGrWt').val(totalGrWt.toFixed(2));
        $('#totalLess').val(totalLess.toFixed(2));
        $('#totalNetWt').val(totalNetWt.toFixed(2));
        $('#totalFine').val(totalFine.toFixed(2));
        $('#totalAmount').val(totalAmount.toFixed(2));
    }

    // Event listeners for input changes
    $('#openingStockTable').on('input', '.weight-input, .less-input, .tunch-input, .wastage-input, .rate-input', function () {
        const row = $(this).closest('tr');
        calculateRow(row);
    });

    // Add new row
    $('.add-row-btn').on('click', function () {
        console.log("Adding new row...");
        const rowCount = $('#openingStockTable tbody tr').length;
        const userId = window.openingStockConfig.userId;
        const itemOptions = window.openingStockConfig.itemOptions;

        const newRow = `
            <tr style="transition: all 0.3s ease; border-bottom: 1px solid #dee2e6;">
                <td style="padding: 10px;">
                    <input type="hidden" name="Items[${rowCount}].Id" />
                    <input type="hidden" name="Items[${rowCount}].UserId" value="${userId}" />
                    <select name="Items[${rowCount}].ItemId" class="form-control custom-select item-select" required style="border-radius: 10px; padding: 8px; border: 1px solid #ced4da; box-shadow: inset 0 1px 3px rgba(0, 0, 0, 0.05); transition: all 0.3s ease;">
                        <option value="">Select Item</option>
                        ${itemOptions}
                    </select>
                    <span class="text-danger" style="font-size: 0.9em;"></span>
                </td>
                <td style="padding: 10px;">
                    <input type="number" name="Items[${rowCount}].Pc" class="form-control pc-input" min="0" step="1" value="" style="border-radius: 10px; padding: 8px; border: 1px solid #ced4da; transition: all 0.3s ease;" />
                    <span class="text-danger" style="font-size: 0.9em;"></span>
                </td>
                <td style="padding: 10px;">
                    <input type="number" name="Items[${rowCount}].Weight" class="form-control weight-input" min="0" step="0.01" value="" style="border-radius: 10px; padding: 8px; border: 1px solid #ced4da; transition: all 0.3s ease;" />
                    <span class="text-danger" style="font-size: 0.9em;"></span>
                </td>
                <td style="padding: 10px;">
                    <input type="number" name="Items[${rowCount}].Less" class="form-control less-input" min="0" step="0.01" value="" style="border-radius: 10px; padding: 8px; border: 1px solid #ced4da; transition: all 0.3s ease;" />
                    <span class="text-danger" style="font-size: 0.9em;"></span>
                </td>
                <td style="padding: 10px;">
                    <input type="number" name="Items[${rowCount}].NetWt" class="form-control netwt-input" step="0.01" readonly value="0.00" style="border-radius: 10px; padding: 8px; background: #e9ecef; border: none;" />
                    <span class="text-danger" style="font-size: 0.9em;"></span>
                </td>
                <td style="padding: 10px;">
                    <input type="number" name="Items[${rowCount}].Tunch" class="form-control tunch-input" min="0" step="0.01" value="" style="border-radius: 10px; padding: 8px; border: 1px solid #ced4da; transition: all 0.3s ease;" />
                    <span class="text-danger" style="font-size: 0.9em;"></span>
                </td>
                <td style="padding: 10px;">
                    <input type="number" name="Items[${rowCount}].Wastage" class="form-control wastage-input" min="0" step="0.01" value="" style="border-radius: 10px; padding: 8px; border: 1px solid #ced4da; transition: all 0.3s ease;" />
                    <span class="text-danger" style="font-size: 0.9em;"></span>
                </td>
                <td style="padding: 10px;">
                    <input type="number" name="Items[${rowCount}].TW" class="form-control tw-input" step="0.01" readonly value="0.00" style="border-radius: 10px; padding: 8px; background: #e9ecef; border: none;" />
                    <span class="text-danger" style="font-size: 0.9em;"></span>
                </td>
                <td style="padding: 10px;">
                    <input type="number" name="Items[${rowCount}].Rate" class="form-control rate-input" min="0" step="0.01" value="" style="border-radius: 10px; padding: 8px; border: 1px solid #ced4da; transition: all 0.3s ease;" />
                    <span class="text-danger" style="font-size: 0.9em;"></span>
                </td>
                <td style="padding: 10px;">
                    <input type="number" name="Items[${rowCount}].Fine" class="form-control fine-input" step="0.01" readonly value="0.00" style="border-radius: 10px; padding: 8px; background: #e9ecef; border: none;" />
                    <span class="text-danger" style="font-size: 0.9em;"></span>
                </td>
                <td style="padding: 10px;">
                    <input type="number" name="Items[${rowCount}].Amount" class="form-control amount-input" step="0.01" readonly value="0.00" style="border-radius: 10px; padding: 8px; background: #e9ecef; border: none;" />
                    <input type="hidden" name="Items[${rowCount}].Amount" class="hidden-amount-input" value="0.00" />
                    <span class="text-danger" style="font-size: 0.9em;"></span>
                </td>
                <td style="padding: 10px;">
                    <button type="button" class="btn btn-danger btn-sm remove-row-btn" style="border-radius: 50%; padding: 8px 12px; background: linear-gradient(to right, #dc3545, #ff6b6b); border: none; transition: all 0.3s ease; box-shadow: 0 2px 5px rgba(0, 0, 0, 0.1);"><i class="bi bi-trash"></i></button>
                </td>
            </tr>
        `;
        $('#openingStockTable tbody').append(newRow);
    });

    // Remove row
    $('#openingStockTable').on('click', '.remove-row-btn', function () {
        console.log("Removing row...");
        $(this).closest('tr').remove();
        updateTotals();
    });

    // Initial calculation for existing rows
    $('#openingStockTable tbody tr').each(function () {
        calculateRow($(this));
    });

    // Delete Bill
    $('#deleteBill').on('click', function () {
        const billNo = $(this).data('billno');
        Swal.fire({
            title: 'Are you sure?',
            text: "You won't be able to revert this!",
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#3085d6',
            cancelButtonColor: '#d33',
            confirmButtonText: 'Yes, delete it!'
        }).then((result) => {
            if (result.isConfirmed) {
                $.ajax({
                    url: '/OpeningStock/DeleteOpeningStock',
                    type: 'POST',
                    contentType: 'application/json',
                    data: JSON.stringify({ BillNo: billNo }),
                    headers: {
                        'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
                    },
                    success: function (response) {
                        if (response.success) {
                            Swal.fire({
                                icon: 'success',
                                title: 'Deleted!',
                                text: response.message,
                                confirmButtonText: 'OK'
                            }).then(() => {
                                window.location.href = '/OpeningStock/ViewStock';
                            });
                        } else {
                            Swal.fire('Error', response.error, 'error');
                        }
                    },
                    error: function () {
                        Swal.fire('Error', 'An error occurred while deleting the bill.', 'error');
                    }
                });
            }
        });
    });

    // Export to PDF
    $('#exportPdf').on('click', function () {
        exportToPDF(); // Reuse the function from transaction.js
    });
});