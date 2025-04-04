function setupEventListeners() {
    document.getElementById('accountSelect').addEventListener('change', function () {
        updatePreviousBalance();
        syncAccountIds();
        const firstItemSelect = document.querySelector('.item-select');
        if (firstItemSelect) firstItemSelect.focus();
    });

    document.getElementById('transactionTable').addEventListener('change', function (e) {
        const target = e.target;
        const row = target.closest('tr');
        if (!row) return;

        if (target.classList.contains('item-select')) {
            handleItemSelection(row, target);
        } else if (target.classList.contains('pc-input') || target.classList.contains('weight-input') ||
            target.classList.contains('less-input') || target.classList.contains('tunch-input') ||
            target.classList.contains('wastage-input') || target.classList.contains('rate-input')) {
            calculateRow(row);
            handleAutoFocus(row, target);
        }
    });

    document.getElementById('transactionTable').addEventListener('input', function (e) {
        const target = e.target;
        const row = target.closest('tr');
        if (!row) return;

        if (target.classList.contains('pc-input') || target.classList.contains('weight-input') ||
            target.classList.contains('less-input') || target.classList.contains('tunch-input') ||
            target.classList.contains('wastage-input') || target.classList.contains('rate-input')) {
            calculateRow(row);
        }
    });

    updatePreviousBalance();
    calculateAllRows();
}

function handleItemSelection(row, itemSelect) {
    const group = itemSelect.options[itemSelect.selectedIndex]?.dataset.group || '';
    const pcInput = row.querySelector('.pc-input');
    const weightInput = row.querySelector('.weight-input');
    const lessInput = row.querySelector('.less-input');
    const tunchInput = row.querySelector('.tunch-input');
    const wastageInput = row.querySelector('.wastage-input');
    const rateInput = row.querySelector('.rate-input');

    // Reset all fields to default state
    pcInput.disabled = false;
    pcInput.required = false;
    weightInput.disabled = false;
    weightInput.required = false;
    lessInput.disabled = false;
    lessInput.required = false;
    tunchInput.disabled = false;
    tunchInput.required = false;
    wastageInput.disabled = false;
    wastageInput.required = false;
    rateInput.disabled = false;
    rateInput.required = false;

    // Apply rules based on item group
    switch (group) {
        case 'Gold Jewelry':
            pcInput.disabled = true;
            pcInput.value = '';
            weightInput.required = true;
            lessInput.required = false; // Optional
            tunchInput.required = true;
            wastageInput.required = true;
            rateInput.disabled = true;
            rateInput.value = '';
            weightInput.focus();
            break;
        case 'PC Gold Jewelry':
            pcInput.required = true;
            weightInput.disabled = true;
            weightInput.value = '';
            lessInput.disabled = true;
            lessInput.value = '';
            tunchInput.disabled = true;
            tunchInput.value = '';
            wastageInput.disabled = true;
            wastageInput.value = '';
            rateInput.required = true;
            pcInput.focus();
            break;
        case 'PC/Weight Jewelry':
            pcInput.required = true;
            weightInput.required = true;
            lessInput.required = false; // Optional
            tunchInput.required = true;
            wastageInput.required = true;
            rateInput.required = true;
            pcInput.focus();
            break;
        default:
            // If no valid group, disable all fields except item selection
            pcInput.disabled = true;
            pcInput.value = '';
            weightInput.disabled = true;
            weightInput.value = '';
            lessInput.disabled = true;
            lessInput.value = '';
            tunchInput.disabled = true;
            tunchInput.value = '';
            wastageInput.disabled = true;
            wastageInput.value = '';
            rateInput.disabled = true;
            rateInput.value = '';
            break;
    }

    calculateRow(row);
}

function handleAutoFocus(row, input) {
    const group = row.querySelector('.item-select').options[row.querySelector('.item-select').selectedIndex]?.dataset.group || '';
    const pcInput = row.querySelector('.pc-input');
    const weightInput = row.querySelector('.weight-input');
    const lessInput = row.querySelector('.less-input');
    const tunchInput = row.querySelector('.tunch-input');
    const wastageInput = row.querySelector('.wastage-input');
    const rateInput = row.querySelector('.rate-input');

    switch (group) {
        case 'Gold Jewelry':
            if (input.classList.contains('weight-input')) {
                lessInput.focus();
            } else if (input.classList.contains('less-input')) {
                tunchInput.focus();
            } else if (input.classList.contains('tunch-input')) {
                wastageInput.focus();
            }
            break;
        case 'PC Gold Jewelry':
            if (input.classList.contains('pc-input')) {
                rateInput.focus();
            }
            break;
        case 'PC/Weight Jewelry':
            if (input.classList.contains('pc-input')) {
                weightInput.focus();
            } else if (input.classList.contains('weight-input')) {
                lessInput.focus();
            } else if (input.classList.contains('less-input')) {
                tunchInput.focus();
            } else if (input.classList.contains('tunch-input')) {
                wastageInput.focus();
            } else if (input.classList.contains('wastage-input')) {
                rateInput.focus();
            }
            break;
    }
}

function calculateRow(row) {
    const pc = parseFloat(row.querySelector('.pc-input').value) || 0;
    const weight = parseFloat(row.querySelector('.weight-input').value) || 0;
    const less = parseFloat(row.querySelector('.less-input').value) || 0;
    const netWt = weight - less;
    const tunch = parseFloat(row.querySelector('.tunch-input').value) || 0;
    const wastage = parseFloat(row.querySelector('.wastage-input').value) || 0;
    const tw = tunch + wastage;
    const rate = parseFloat(row.querySelector('.rate-input').value) || 0;

    let fine = 0;
    let amount = 0;
    const group = row.querySelector('.item-select').options[row.querySelector('.item-select').selectedIndex]?.dataset.group || '';

    switch (group) {
        case 'Gold Jewelry':
            fine = (netWt * tw) / 100;
            amount = fine * rate;
            break;
        case 'PC Gold Jewelry':
            amount = pc * rate;
            break;
        case 'PC/Weight Jewelry':
            fine = (netWt * tw) / 100;
            amount = pc * rate;
            break;
    }

    row.querySelector('.netwt-input').value = netWt.toFixed(2);
    row.querySelector('.tw-input').value = tw.toFixed(2);
    row.querySelector('.fine-input').value = fine.toFixed(2);
    row.querySelector('.amount-input').value = amount.toFixed(2);
    row.querySelector('.hidden-amount-input').value = amount.toFixed(2);

    calculateTotals();
}

function calculateTotals() {
    let totalGrWt = 0, totalLess = 0, totalNetWt = 0, totalFine = 0, totalAmount = 0;

    document.querySelectorAll('#transactionTable tbody tr').forEach(row => {
        totalGrWt += parseFloat(row.querySelector('.weight-input').value) || 0;
        totalLess += parseFloat(row.querySelector('.less-input').value) || 0;
        totalNetWt += parseFloat(row.querySelector('.netwt-input').value) || 0;
        totalFine += parseFloat(row.querySelector('.fine-input').value) || 0;
        totalAmount += parseFloat(row.querySelector('.amount-input').value) || 0;
    });

    document.getElementById('totalGrWt').value = totalGrWt.toFixed(2);
    document.getElementById('totalLess').value = totalLess.toFixed(2);
    document.getElementById('totalNetWt').value = totalNetWt.toFixed(2);
    document.getElementById('totalFine').value = totalFine.toFixed(2);
    document.getElementById('totalAmount').value = totalAmount.toFixed(2);

    document.getElementById('currentFine').value = totalFine.toFixed(2);
    document.getElementById('currentAmount').value = totalAmount.toFixed(2);

    updateTotalBalance();
}

function calculateAllRows() {
    document.querySelectorAll('#transactionTable tbody tr').forEach(row => calculateRow(row));
}

function addNewRow(transactionType) {
    const tbody = document.getElementById('transactionTable').querySelector('tbody');
    const rowCount = tbody.querySelectorAll('tr').length;
    const newRow = document.createElement('tr');
    newRow.innerHTML = `
        <td>
            <input type="hidden" name="Items[${rowCount}].Id" value="0" />
            <input type="hidden" name="Items[${rowCount}].TransactionType" value="${transactionType}" />
            <input type="hidden" name="Items[${rowCount}].AccountId" class="account-id-input" />
            <input type="hidden" name="Items[${rowCount}].UserId" value="${document.querySelector('input[name="UserId"]').value}" />
            <select name="Items[${rowCount}].ItemId" class="form-control item-select" required>
                <option value="">Select Item</option>
                ${Array.from(document.querySelector('.item-select').options)
            .filter(option => option.value)
            .map(option => `<option value="${option.value}" data-group="${option.dataset.group}">${option.text}</option>`)
            .join('')}
            </select>
            <span class="text-danger" data-valmsg-for="Items[${rowCount}].ItemId" data-valmsg-replace="true"></span>
        </td>
        <td>
            <input type="number" name="Items[${rowCount}].Pc" class="form-control pc-input" min="0" step="1" value="" />
            <span class="text-danger" data-valmsg-for="Items[${rowCount}].Pc" data-valmsg-replace="true"></span>
        </td>
        <td>
            <input type="number" name="Items[${rowCount}].Weight" class="form-control weight-input" min="0" step="0.01" value="" />
            <span class="text-danger" data-valmsg-for="Items[${rowCount}].Weight" data-valmsg-replace="true"></span>
        </td>
        <td>
            <input type="number" name="Items[${rowCount}].Less" class="form-control less-input" min="0" step="0.01" value="" />
            <span class="text-danger" data-valmsg-for="Items[${rowCount}].Less" data-valmsg-replace="true"></span>
        </td>
        <td>
            <input type="number" name="Items[${rowCount}].NetWt" class="form-control netwt-input" step="0.01" readonly value="0.00" />
            <span class="text-danger" data-valmsg-for="Items[${rowCount}].NetWt" data-valmsg-replace="true"></span>
        </td>
        <td>
            <input type="number" name="Items[${rowCount}].Tunch" class="form-control tunch-input" min="0" step="0.01" value="" />
            <span class="text-danger" data-valmsg-for="Items[${rowCount}].Tunch" data-valmsg-replace="true"></span>
        </td>
        <td>
            <input type="number" name="Items[${rowCount}].Wastage" class="form-control wastage-input" min="0" step="0.01" value="" />
            <span class="text-danger" data-valmsg-for="Items[${rowCount}].Wastage" data-valmsg-replace="true"></span>
        </td>
        <td>
            <input type="number" name="Items[${rowCount}].TW" class="form-control tw-input" step="0.01" readonly value="0.00" />
            <span class="text-danger" data-valmsg-for="Items[${rowCount}].TW" data-valmsg-replace="true"></span>
        </td>
        <td>
            <input type="number" name="Items[${rowCount}].Rate" class="form-control rate-input" min="0" step="0.01" value="" />
            <span class="text-danger" data-valmsg-for="Items[${rowCount}].Rate" data-valmsg-replace="true"></span>
        </td>
        <td>
            <input type="number" name="Items[${rowCount}].Fine" class="form-control fine-input" step="0.01" readonly value="0.00" />
            <span class="text-danger" data-valmsg-for="Items[${rowCount}].Fine" data-valmsg-replace="true"></span>
        </td>
        <td>
            <input type="number" class="form-control amount-input" step="0.01" readonly value="0.00" />
            <input type="hidden" name="Items[${rowCount}].Amount" class="hidden-amount-input" value="0.00" />
            <span class="text-danger" data-valmsg-for="Items[${rowCount}].Amount" data-valmsg-replace="true"></span>
        </td>
        <td>
            <button type="button" class="btn btn-danger btn-sm" onclick="removeRow(this)">Remove</button>
        </td>
    `;
    tbody.appendChild(newRow);
    newRow.querySelector('.item-select').focus();
}

function removeRow(button) {
    button.closest('tr').remove();
    calculateTotals();
}

function syncAccountIds() {
    const accountId = document.getElementById('accountSelect').value;
    document.querySelectorAll('.account-id-input').forEach(input => {
        input.value = accountId;
    });
}

function updatePreviousBalance() {
    const accountId = document.getElementById('accountSelect').value;
    if (!accountId) {
        document.getElementById('prevFine').value = '0.00';
        document.getElementById('prevAmount').value = '0.00';
        document.getElementById('prevDate').textContent = '';
        updateTotalBalance();
        return;
    }

    fetch(`/Transaction/GetPreviousBalance?accountId=${accountId}`, { // Removed unused transactionType param
        headers: { 'X-Requested-With': 'XMLHttpRequest' }
    })
        .then(response => response.json())
        .then(data => {
            document.getElementById('prevFine').value = data.fine.toFixed(2);
            document.getElementById('prevAmount').value = data.amount.toFixed(2);
            document.getElementById('prevDate').textContent = data.date ? `Last Updated: ${new Date(data.date).toLocaleDateString()}` : '';
            updateTotalBalance();
        })
        .catch(error => {
            console.error('Error fetching previous balance:', error);
            document.getElementById('prevFine').value = '0.00';
            document.getElementById('prevAmount').value = '0.00';
            document.getElementById('prevDate').textContent = 'Error fetching balance';
            updateTotalBalance();
        });
}

function updateTotalBalance() {
    const prevFine = parseFloat(document.getElementById('prevFine').value) || 0;
    const prevAmount = parseFloat(document.getElementById('prevAmount').value) || 0;
    const currentFine = parseFloat(document.getElementById('currentFine').value) || 0;
    const currentAmount = parseFloat(document.getElementById('currentAmount').value) || 0;

    const totalFine = prevFine + (['Sale', 'PurchaseReturn'].includes(window.transactionType) ? -currentFine : currentFine);
    const totalAmount = prevAmount + (['Sale', 'PurchaseReturn'].includes(window.transactionType) ? -currentAmount : currentAmount);

    document.getElementById('totalBalanceFine').value = totalFine.toFixed(2);
    document.getElementById('totalBalanceAmount').value = totalAmount.toFixed(2);
}

function setupFormSubmission(addTransactionUrl) {
    document.getElementById('transactionForm').addEventListener('submit', function (e) {
        e.preventDefault();
        syncAccountIds();
        let isFormValid = Array.from(document.querySelectorAll('#transactionTable tbody tr')).every(row => validateRow(row.querySelector('.item-select'), true));

        if (!isFormValid) {
            Swal.fire({
                icon: 'error',
                title: 'Validation Error',
                text: 'Please fill all required fields correctly.',
            });
        } else {
            const formData = new FormData(this);
            fetch(addTransactionUrl, {
                method: 'POST',
                body: formData,
                headers: { 'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value }
            })
                .then(response => response.json())
                .then(data => {
                    if (data.success) {
                        Swal.fire({
                            icon: 'success',
                            title: 'Success',
                            text: data.message || 'Transaction saved successfully!',
                            timer: 1500,
                            showConfirmButton: false
                        }).then(() => window.location.href = data.redirectUrl);
                    } else {
                        Swal.fire({
                            icon: 'error',
                            title: 'Error',
                            text: data.error || 'Failed to save the transaction.'
                        });
                    }
                })
                .catch(error => {
                    console.error('Error during save:', error);
                    Swal.fire({
                        icon: 'error',
                        title: 'Error',
                        text: 'An error occurred while saving.'
                    });
                });
        }
    });
}

function validateRow(itemSelect, showAlert = false) {
    const row = itemSelect.closest('tr');
    const group = itemSelect.options[itemSelect.selectedIndex]?.dataset.group || '';
    let isValid = true;

    const setError = (input, message) => {
        const errorSpan = input.nextElementSibling;
        if (errorSpan && errorSpan.classList.contains('text-danger')) {
            errorSpan.textContent = message;
        }
        isValid = false;
    };

    const clearError = (input) => {
        const errorSpan = input.nextElementSibling;
        if (errorSpan && errorSpan.classList.contains('text-danger')) {
            errorSpan.textContent = '';
        }
    };

    const pcInput = row.querySelector('.pc-input');
    const weightInput = row.querySelector('.weight-input');
    const tunchInput = row.querySelector('.tunch-input');
    const wastageInput = row.querySelector('.wastage-input');
    const rateInput = row.querySelector('.rate-input');

    if (!itemSelect.value) {
        setError(itemSelect, 'Item is required.');
        return false;
    } else {
        clearError(itemSelect);
    }

    switch (group) {
        case 'Gold Jewelry':
            if (!weightInput.value || parseFloat(weightInput.value) <= 0) {
                setError(weightInput, 'Gross Weight is required.');
            } else {
                clearError(weightInput);
            }
            if (!tunchInput.value || parseFloat(tunchInput.value) <= 0) {
                setError(tunchInput, 'Tunch is required.');
            } else {
                clearError(tunchInput);
            }
            if (!wastageInput.value || parseFloat(wastageInput.value) <= 0) {
                setError(wastageInput, 'Wastage is required.');
            } else {
                clearError(wastageInput);
            }
            break;
        case 'PC Gold Jewelry':
            if (!pcInput.value || parseFloat(pcInput.value) <= 0) {
                setError(pcInput, 'Pc is required.');
            } else {
                clearError(pcInput);
            }
            if (!rateInput.value || parseFloat(rateInput.value) <= 0) {
                setError(rateInput, 'Rate is required.');
            } else {
                clearError(rateInput);
            }
            break;
        case 'PC/Weight Jewelry':
            if (!pcInput.value || parseFloat(pcInput.value) <= 0) {
                setError(pcInput, 'Pc is required.');
            } else {
                clearError(pcInput);
            }
            if (!weightInput.value || parseFloat(weightInput.value) <= 0) {
                setError(weightInput, 'Gross Weight is required.');
            } else {
                clearError(weightInput);
            }
            if (!tunchInput.value || parseFloat(tunchInput.value) <= 0) {
                setError(tunchInput, 'Tunch is required.');
            } else {
                clearError(tunchInput);
            }
            if (!wastageInput.value || parseFloat(wastageInput.value) <= 0) {
                setError(wastageInput, 'Wastage is required.');
            } else {
                clearError(wastageInput);
            }
            if (!rateInput.value || parseFloat(rateInput.value) <= 0) {
                setError(rateInput, 'Rate is required.');
            } else {
                clearError(rateInput);
            }
            break;
    }

    return isValid;
}

function printTransaction() {
    window.print();
}

function exportToExcel() {
    const table = document.getElementById('transactionTable');
    const rows = Array.from(table.querySelectorAll('tr'));
    const csv = rows.map(row => Array.from(row.cells).slice(0, -1).map(cell => `"${cell.textContent.trim()}"`).join(',')).join('\n');
    const blob = new Blob([csv], { type: 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'transaction.csv';
    a.click();
    window.URL.revokeObjectURL(url);
}

function exportToPDF() {
    const { jsPDF } = window.jspdf;
    const doc = new jsPDF();
    doc.autoTable({ html: '#transactionTable' });
    doc.save('transaction.pdf');
}

function deleteTransaction(billNo) {
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
            fetch(`/Transaction/DeleteTransaction?billNo=${billNo}`, {
                method: 'POST',
                headers: { 'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value }
            })
                .then(response => response.json())
                .then(data => {
                    if (data.success) {
                        Swal.fire('Deleted!', 'Transaction has been deleted.', 'success')
                            .then(() => window.location.href = data.redirectUrl);
                    } else {
                        Swal.fire('Error!', data.error || 'Failed to delete the transaction.', 'error');
                    }
                })
                .catch(error => {
                    console.error('Error during delete:', error);
                    Swal.fire('Error!', 'An error occurred while deleting.', 'error');
                });
        }
    });
}