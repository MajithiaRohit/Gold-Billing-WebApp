function setupEventListeners() {
    const table = document.getElementById('openingStockTable');

    // Handle item selection on change
    table.addEventListener('change', function (e) {
        const target = e.target;
        const row = target.closest('tr');
        if (!row) return;

        if (target.classList.contains('item-select')) {
            handleItemSelection(row, target);
        }
    });

    // Real-time calculation on input
    table.addEventListener('input', function (e) {
        const target = e.target;
        const row = target.closest('tr');
        if (!row) return;

        if (target.classList.contains('pc-input') || target.classList.contains('weight-input') ||
            target.classList.contains('less-input') || target.classList.contains('tunch-input') ||
            target.classList.contains('wastage-input') || target.classList.contains('rate-input')) {
            calculateRow(row);
        }
    });

    // Auto-focus on Tab or Enter key press
    table.addEventListener('keydown', function (e) {
        const target = e.target;
        const row = target.closest('tr');
        if (!row) return;

        // Only move focus on Tab (key code 9) or Enter (key code 13)
        if (e.key === 'Tab' || e.key === 'Enter') {
            if (target.classList.contains('pc-input') || target.classList.contains('weight-input') ||
                target.classList.contains('less-input') || target.classList.contains('tunch-input') ||
                target.classList.contains('wastage-input') || target.classList.contains('rate-input')) {
                e.preventDefault(); // Prevent default Tab behavior
                calculateRow(row); // Ensure calculation before moving focus
                handleAutoFocus(row, target);
            }
        }
    });

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
            lessInput.required = false;
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
            lessInput.required = false;
            tunchInput.required = true;
            wastageInput.required = true;
            rateInput.required = true;
            pcInput.focus();
            break;
        default:
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
            if (input.classList.contains('weight-input') && weightInput.value) lessInput.focus();
            else if (input.classList.contains('less-input') && lessInput.value) tunchInput.focus();
            else if (input.classList.contains('tunch-input') && tunchInput.value) wastageInput.focus();
            break;
        case 'PC Gold Jewelry':
            if (input.classList.contains('pc-input') && pcInput.value) rateInput.focus();
            break;
        case 'PC/Weight Jewelry':
            if (input.classList.contains('pc-input') && pcInput.value) weightInput.focus();
            else if (input.classList.contains('weight-input') && weightInput.value) lessInput.focus();
            else if (input.classList.contains('less-input') && lessInput.value) tunchInput.focus();
            else if (input.classList.contains('tunch-input') && tunchInput.value) wastageInput.focus();
            else if (input.classList.contains('wastage-input') && wastageInput.value) rateInput.focus();
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

    document.querySelectorAll('#openingStockTable tbody tr').forEach(row => {
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
}

function calculateAllRows() {
    document.querySelectorAll('#openingStockTable tbody tr').forEach(row => calculateRow(row));
}

function addNewRow() {
    const tbody = document.getElementById('openingStockTable').querySelector('tbody');
    const rowCount = tbody.querySelectorAll('tr').length;
    const newRow = document.createElement('tr');
    newRow.innerHTML = `
        <td style="padding: 10px;">
            <input type="hidden" name="Items[${rowCount}].Id" value="0" />
            <input type="hidden" name="Items[${rowCount}].UserId" value="${document.querySelector('input[name="UserId"]').value}" />
            <select name="Items[${rowCount}].ItemId" class="form-control custom-select item-select" required style="border-radius: 10px; padding: 8px; border: 1px solid #ced4da; box-shadow: inset 0 1px 3px rgba(0, 0, 0, 0.05); transition: all 0.3s ease;">
                <option value="">Select Item</option>
                ${Array.from(document.querySelector('.item-select').options)
            .filter(option => option.value)
            .map(option => `<option value="${option.value}" data-group="${option.dataset.group}">${option.text}</option>`)
            .join('')}
            </select>
            <span class="text-danger" data-valmsg-for="Items[${rowCount}].ItemId" data-valmsg-replace="true"></span>
        </td>
        <td style="padding: 10px;">
            <input type="number" name="Items[${rowCount}].Pc" class="form-control pc-input" min="0" step="1" value="" style="border-radius: 10px; padding: 8px; border: 1px solid #ced4da; transition: all 0.3s ease;" />
            <span class="text-danger" data-valmsg-for="Items[${rowCount}].Pc" data-valmsg-replace="true"></span>
        </td>
        <td style="padding: 10px;">
            <input type="number" name="Items[${rowCount}].Weight" class="form-control weight-input" min="0" step="0.01" value="" style="border-radius: 10px; padding: 8px; border: 1px solid #ced4da; transition: all 0.3s ease;" />
            <span class="text-danger" data-valmsg-for="Items[${rowCount}].Weight" data-valmsg-replace="true"></span>
        </td>
        <td style="padding: 10px;">
            <input type="number" name="Items[${rowCount}].Less" class="form-control less-input" min="0" step="0.01" value="" style="border-radius: 10px; padding: 8px; border: 1px solid #ced4da; transition: all 0.3s ease;" />
            <span class="text-danger" data-valmsg-for="Items[${rowCount}].Less" data-valmsg-replace="true"></span>
        </td>
        <td style="padding: 10px;">
            <input type="number" name="Items[${rowCount}].NetWt" class="form-control netwt-input" step="0.01" readonly value="0.00" style="border-radius: 10px; padding: 8px; background: #e9ecef; border: none;" />
            <span class="text-danger" data-valmsg-for="Items[${rowCount}].NetWt" data-valmsg-replace="true"></span>
        </td>
        <td style="padding: 10px;">
            <input type="number" name="Items[${rowCount}].Tunch" class="form-control tunch-input" min="0" step="0.01" value="" style="border-radius: 10px; padding: 8px; border: 1px solid #ced4da; transition: all 0.3s ease;" />
            <span class="text-danger" data-valmsg-for="Items[${rowCount}].Tunch" data-valmsg-replace="true"></span>
        </td>
        <td style="padding: 10px;">
            <input type="number" name="Items[${rowCount}].Wastage" class="form-control wastage-input" min="0" step="0.01" value="" style="border-radius: 10px; padding: 8px; border: 1px solid #ced4da; transition: all 0.3s ease;" />
            <span class="text-danger" data-valmsg-for="Items[${rowCount}].Wastage" data-valmsg-replace="true"></span>
        </td>
        <td style="padding: 10px;">
            <input type="number" name="Items[${rowCount}].TW" class="form-control tw-input" step="0.01" readonly value="0.00" style="border-radius: 10px; padding: 8px; background: #e9ecef; border: none;" />
            <span class="text-danger" data-valmsg-for="Items[${rowCount}].TW" data-valmsg-replace="true"></span>
        </td>
        <td style="padding: 10px;">
            <input type="number" name="Items[${rowCount}].Rate" class="form-control rate-input" min="0" step="0.01" value="" style="border-radius: 10px; padding: 8px; border: 1px solid #ced4da; transition: all 0.3s ease;" />
            <span class="text-danger" data-valmsg-for="Items[${rowCount}].Rate" data-valmsg-replace="true"></span>
        </td>
        <td style="padding: 10px;">
            <input type="number" name="Items[${rowCount}].Fine" class="form-control fine-input" step="0.01" readonly value="0.00" style="border-radius: 10px; padding: 8px; background: #e9ecef; border: none;" />
            <span class="text-danger" data-valmsg-for="Items[${rowCount}].Fine" data-valmsg-replace="true"></span>
        </td>
        <td style="padding: 10px;">
            <input type="number" class="form-control amount-input" step="0.01" readonly value="0.00" />
            <input type="hidden" name="Items[${rowCount}].Amount" class="hidden-amount-input" value="0.00" />
            <span class="text-danger" data-valmsg-for="Items[${rowCount}].Amount" data-valmsg-replace="true"></span>
        </td>
        <td style="padding: 10px;">
            <button type="button" class="btn btn-danger btn-sm remove-row-btn" style="border-radius: 50%; padding: 8px 12px; background: linear-gradient(to right, #dc3545, #ff6b6b); border: none; transition: all 0.3s ease; box-shadow: 0 2px 5px rgba(0, 0, 0, 0.1);"><i class="bi bi-trash"></i></button>
        </td>
    `;
    tbody.appendChild(newRow);
    newRow.querySelector('.item-select').focus();
}

function removeRow(button) {
    button.closest('tr').remove();
    calculateTotals();
}

function setupFormSubmission(addOpeningStockUrl) {
    document.getElementById('openingStockForm').addEventListener('submit', function (e) {
        e.preventDefault();
        let isFormValid = Array.from(document.querySelectorAll('#openingStockTable tbody tr')).every(row => validateRow(row.querySelector('.item-select'), true));

        if (!isFormValid) {
            Swal.fire({
                icon: 'error',
                title: 'Validation Error',
                text: 'Please fill all required fields correctly.',
            });
        } else {
            const formData = new FormData(this);
            fetch(addOpeningStockUrl, {
                method: 'POST',
                body: formData,
                headers: { 'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value }
            })
                .then(response => response.json()) // Parse JSON response
                .then(data => {
                    if (data.success) {
                        window.location.href = data.redirectUrl; // Use URL from server
                    } else {
                        throw new Error(data.error);
                    }
                })
                .catch(error => {
                    console.error('Error during save:', error);
                    Swal.fire({
                        icon: 'error',
                        title: 'Error',
                        text: error.message || 'An error occurred while saving.'
                    });
                });
        }
    });

    document.querySelectorAll('.remove-row-btn').forEach(btn => {
        btn.addEventListener('click', function () {
            removeRow(this);
        });
    });

    document.querySelector('.add-row-btn').addEventListener('click', addNewRow);
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
            if (!weightInput.value || parseFloat(weightInput.value) <= 0) setError(weightInput, 'Gross Weight is required.');
            else clearError(weightInput);
            if (!tunchInput.value || parseFloat(tunchInput.value) <= 0) setError(tunchInput, 'Tunch is required.');
            else clearError(tunchInput);
            if (!wastageInput.value || parseFloat(wastageInput.value) <= 0) setError(wastageInput, 'Wastage is required.');
            else clearError(wastageInput);
            break;
        case 'PC Gold Jewelry':
            if (!pcInput.value || parseFloat(pcInput.value) <= 0) setError(pcInput, 'Pc is required.');
            else clearError(pcInput);
            if (!rateInput.value || parseFloat(rateInput.value) <= 0) setError(rateInput, 'Rate is required.');
            else clearError(rateInput);
            break;
        case 'PC/Weight Jewelry':
            if (!pcInput.value || parseFloat(pcInput.value) <= 0) setError(pcInput, 'Pc is required.');
            else clearError(pcInput);
            if (!weightInput.value || parseFloat(weightInput.value) <= 0) setError(weightInput, 'Gross Weight is required.');
            else clearError(weightInput);
            if (!tunchInput.value || parseFloat(tunchInput.value) <= 0) setError(tunchInput, 'Tunch is required.');
            else clearError(tunchInput);
            if (!wastageInput.value || parseFloat(wastageInput.value) <= 0) setError(wastageInput, 'Wastage is required.');
            else clearError(wastageInput);
            if (!rateInput.value || parseFloat(rateInput.value) <= 0) setError(rateInput, 'Rate is required.');
            else clearError(rateInput);
            break;
    }

    return isValid;
}