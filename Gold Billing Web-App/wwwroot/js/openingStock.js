function setupEventListeners() {
    const table = document.getElementById('openingStockTable');
    if (!table) return;

    table.addEventListener('change', function (e) {
        const target = e.target;
        const row = target.closest('tr');
        if (!row) return;

        if (target.classList.contains('item-select')) {
            handleItemSelection(row, target);
        }
    });

    table.addEventListener('input', function (e) {
        const target = e.target;
        const row = target.closest('tr');
        if (!row) return;

        if (['pc-input', 'weight-input', 'less-input', 'tunch-input', 'wastage-input', 'rate-input'].some(cls => target.classList.contains(cls))) {
            calculateRow(row);
        }
    });

    table.addEventListener('keydown', function (e) {
        const target = e.target;
        const row = target.closest('tr');
        if (!row) return;

        if (e.key === 'Tab' || e.key === 'Enter') {
            e.preventDefault();
            calculateRow(row);
            handleAutoFocus(row, target);
        }
    });

    calculateAllRows();
}

function handleItemSelection(row, itemSelect) {
    const group = itemSelect.options[itemSelect.selectedIndex]?.dataset.group || '';
    const inputs = {
        pc: row.querySelector('.pc-input'),
        weight: row.querySelector('.weight-input'),
        less: row.querySelector('.less-input'),
        tunch: row.querySelector('.tunch-input'),
        wastage: row.querySelector('.wastage-input'),
        rate: row.querySelector('.rate-input')
    };

    Object.values(inputs).forEach(input => {
        if (input) {
            input.disabled = false;
            input.required = false;
            input.value = input.value || '';
        }
    });

    switch (group) {
        case 'Gold Jewelry':
            if (inputs.pc) inputs.pc.disabled = true, inputs.pc.value = '';
            if (inputs.weight) inputs.weight.required = true;
            if (inputs.tunch) inputs.tunch.required = true;
            if (inputs.wastage) inputs.wastage.required = true;
            if (inputs.rate) inputs.rate.disabled = true, inputs.rate.value = '';
            if (inputs.weight) inputs.weight.focus();
            break;
        case 'PC Gold Jewelry':
            if (inputs.pc) inputs.pc.required = true;
            if (inputs.weight) inputs.weight.disabled = true, inputs.weight.value = '';
            if (inputs.less) inputs.less.disabled = true, inputs.less.value = '';
            if (inputs.tunch) inputs.tunch.disabled = true, inputs.tunch.value = '';
            if (inputs.wastage) inputs.wastage.disabled = true, inputs.wastage.value = '';
            if (inputs.rate) inputs.rate.required = true;
            if (inputs.pc) inputs.pc.focus();
            break;
        case 'PC/Weight Jewelry':
            if (inputs.pc) inputs.pc.required = true;
            if (inputs.weight) inputs.weight.required = true;
            if (inputs.tunch) inputs.tunch.required = true;
            if (inputs.wastage) inputs.wastage.required = true;
            if (inputs.rate) inputs.rate.required = true;
            if (inputs.pc) inputs.pc.focus();
            break;
        default:
            Object.values(inputs).forEach(input => {
                if (input) input.disabled = true, input.value = '';
            });
            break;
    }

    calculateRow(row);
}

function handleAutoFocus(row, input) {
    const group = row.querySelector('.item-select')?.options[row.querySelector('.item-select').selectedIndex]?.dataset.group || '';
    const inputs = {
        pc: row.querySelector('.pc-input'),
        weight: row.querySelector('.weight-input'),
        less: row.querySelector('.less-input'),
        tunch: row.querySelector('.tunch-input'),
        wastage: row.querySelector('.wastage-input'),
        rate: row.querySelector('.rate-input')
    };

    const focusNext = (current, next) => {
        if (current && current.value && !current.disabled && next && !next.disabled) next.focus();
    };

    switch (group) {
        case 'Gold Jewelry':
            if (input.classList.contains('weight-input')) focusNext(inputs.weight, inputs.less);
            else if (input.classList.contains('less-input')) focusNext(inputs.less, inputs.tunch);
            else if (input.classList.contains('tunch-input')) focusNext(inputs.tunch, inputs.wastage);
            else if (input.classList.contains('wastage-input')) addNewRowIfLast(row);
            break;
        case 'PC Gold Jewelry':
            if (input.classList.contains('pc-input')) focusNext(inputs.pc, inputs.rate);
            else if (input.classList.contains('rate-input')) addNewRowIfLast(row);
            break;
        case 'PC/Weight Jewelry':
            if (input.classList.contains('pc-input')) focusNext(inputs.pc, inputs.weight);
            else if (input.classList.contains('weight-input')) focusNext(inputs.weight, inputs.less);
            else if (input.classList.contains('less-input')) focusNext(inputs.less, inputs.tunch);
            else if (input.classList.contains('tunch-input')) focusNext(inputs.tunch, inputs.wastage);
            else if (input.classList.contains('wastage-input')) focusNext(inputs.wastage, inputs.rate);
            else if (input.classList.contains('rate-input')) addNewRowIfLast(row);
            break;
    }
}

function addNewRowIfLast(row) {
    const tbody = document.getElementById('openingStockTable')?.querySelector('tbody');
    if (tbody && row === tbody.lastElementChild) addNewRow();
}

function calculateRow(row) {
    const pc = parseFloat(row.querySelector('.pc-input')?.value) || 0;
    const weight = parseFloat(row.querySelector('.weight-input')?.value) || 0;
    const less = parseFloat(row.querySelector('.less-input')?.value) || 0;
    const netWt = weight - less;
    const tunch = parseFloat(row.querySelector('.tunch-input')?.value) || 0;
    const wastage = parseFloat(row.querySelector('.wastage-input')?.value) || 0;
    const tw = tunch + wastage;
    const rate = parseFloat(row.querySelector('.rate-input')?.value) || 0;

    let fine = 0;
    let amount = 0;
    const group = row.querySelector('.item-select')?.options[row.querySelector('.item-select').selectedIndex]?.dataset.group || '';

    switch (group) {
        case 'Gold Jewelry':
            fine = (netWt * tw) / 100;
            amount = fine * rate;
            break;
        case 'PC Gold Jewelry':
            fine = 0;
            amount = pc * rate;
            break;
        case 'PC/Weight Jewelry':
            fine = (netWt * tw) / 100;
            amount = fine * rate;
            break;
    }

    const setValue = (selector, value) => {
        const element = row.querySelector(selector);
        if (element) element.value = value.toFixed(2);
    };

    setValue('.netwt-input', netWt);
    setValue('.tw-input', tw);
    setValue('.fine-input', fine);
    setValue('.amount-input', amount);
    setValue('.hidden-amount-input', amount);

    calculateTotals();
}

function calculateTotals() {
    let totalGrWt = 0, totalLess = 0, totalNetWt = 0, totalFine = 0, totalAmount = 0;
    const rows = document.querySelectorAll('#openingStockTable tbody tr') || [];

    rows.forEach(row => {
        totalGrWt += parseFloat(row.querySelector('.weight-input')?.value) || 0;
        totalLess += parseFloat(row.querySelector('.less-input')?.value) || 0;
        totalNetWt += parseFloat(row.querySelector('.netwt-input')?.value) || 0;
        totalFine += parseFloat(row.querySelector('.fine-input')?.value) || 0;
        totalAmount += parseFloat(row.querySelector('.amount-input')?.value) || 0;
    });

    const setTotal = (id, value) => {
        const element = document.getElementById(id);
        if (element) element.value = value.toFixed(2);
    };

    setTotal('totalGrWt', totalGrWt);
    setTotal('totalLess', totalLess);
    setTotal('totalNetWt', totalNetWt);
    setTotal('totalFine', totalFine);
    setTotal('totalAmount', totalAmount);
}

function calculateAllRows() {
    const rows = document.querySelectorAll('#openingStockTable tbody tr') || [];
    rows.forEach(row => calculateRow(row));
}

function addNewRow() {
    const tbody = document.getElementById('openingStockTable')?.querySelector('tbody');
    if (!tbody) return;

    const rowCount = tbody.querySelectorAll('tr').length;
    const newRow = document.createElement('tr');
    newRow.innerHTML = `
        <td style="padding: 10px;">
            <input type="hidden" name="Items[${rowCount}].Id" value="0" />
            <input type="hidden" name="Items[${rowCount}].UserId" value="${document.querySelector('input[name="UserId"]')?.value || ''}" />
            <select name="Items[${rowCount}].ItemId" class="form-control custom-select item-select" required style="border-radius: 10px; padding: 8px; border: 1px solid #ced4da; box-shadow: inset 0 1px 3px rgba(0, 0, 0, 0.05); transition: all 0.3s ease;">
                <option value="">Select Item</option>
                ${Array.from(document.querySelector('.item-select')?.options || [])
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
        <td style="padding: 10px;" class="text-center">
            <button type="button" class="btn btn-danger btn-sm remove-row-btn" style="border-radius: 50%; padding: 8px 12px; background: linear-gradient(to right, #dc3545, #ff6b6b); border: none; transition: all 0.3s ease; box-shadow: 0 2px 5px rgba(0, 0, 0, 0.1);"><i class="bi bi-trash"></i></button>
        </td>
    `;
    tbody.appendChild(newRow);
    const itemSelect = newRow.querySelector('.item-select');
    if (itemSelect) itemSelect.focus();
}

function removeRow(button) {
    const row = button.closest('tr');
    if (row) {
        row.remove();
        calculateTotals();
    }
}

function setupFormSubmission(addOpeningStockUrl, deleteOpeningStockUrl) {
    const form = document.getElementById('openingStockForm');
    if (form) {
        form.addEventListener('submit', function (e) {
            e.preventDefault();
            let isFormValid = Array.from(document.querySelectorAll('#openingStockTable tbody tr') || []).every(row => validateRow(row.querySelector('.item-select'), true));

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
                    headers: { 'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || '' }
                })
                    .then(response => {
                        if (!response.ok) {
                            return response.text().then(text => { throw new Error(`Server returned ${response.status}: ${text}`); });
                        }
                        return response.json();
                    })
                    .then(data => {
                        if (data.success) {
                            Swal.fire({
                                icon: 'success',
                                title: 'Saved!',
                                text: 'Stock has been saved successfully.',
                                timer: 1500,
                                showConfirmButton: false
                            }).then(() => {
                                window.location.href = data.redirectUrl;
                            });
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
    }

    const deleteBtn = document.getElementById('deleteBillBtn');
    if (deleteBtn) {
        deleteBtn.addEventListener('click', function () {
            const billNo = this.getAttribute('data-bill-no');
            Swal.fire({
                title: 'Are you sure?',
                text: `You are about to delete bill ${billNo}. This action cannot be undone.`,
                icon: 'warning',
                showCancelButton: true,
                confirmButtonColor: '#d33',
                cancelButtonColor: '#3085d6',
                confirmButtonText: 'Yes, delete it!'
            }).then((result) => {
                if (result.isConfirmed) {
                    const formData = new FormData();
                    formData.append('billNo', billNo);
                    const tokenElement = document.querySelector('input[name="__RequestVerificationToken"]');
                    const token = tokenElement ? tokenElement.value : '';
                    console.log('Sending delete request for billNo:', billNo);
                    console.log('Anti-forgery token:', token || 'Token not found');
                    if (!token) {
                        Swal.fire({
                            icon: 'error',
                            title: 'Error',
                            text: 'Anti-forgery token missing. Please refresh the page.'
                        });
                        return;
                    }
                    fetch(deleteOpeningStockUrl, {
                        method: 'POST',
                        body: formData,
                        headers: {
                            'RequestVerificationToken': token,
                            'Accept': 'application/json'
                        }
                    })
                        .then(response => {
                            console.log('Response status:', response.status);
                            console.log('Response headers:', [...response.headers.entries()]);
                            if (!response.ok) {
                                return response.text().then(text => { throw new Error(`Server returned ${response.status}: ${text}`); });
                            }
                            return response.json();
                        })
                        .then(data => {
                            if (data.success) {
                                Swal.fire({
                                    icon: 'success',
                                    title: 'Deleted!',
                                    text: `Bill ${billNo} has been deleted.`,
                                    timer: 1500,
                                    showConfirmButton: false
                                }).then(() => {
                                    window.location.href = data.redirectUrl;
                                });
                            } else {
                                throw new Error(data.error || 'Unknown error');
                            }
                        })
                        .catch(error => {
                            console.error('Error during delete:', error);
                            Swal.fire({
                                icon: 'error',
                                title: 'Error',
                                text: error.message || 'An error occurred while deleting.'
                            });
                        });
                }
            });
        });
    }

    const table = document.getElementById('openingStockTable');
    if (table) {
        table.addEventListener('click', function (e) {
            if (e.target.classList.contains('remove-row-btn') || e.target.closest('.remove-row-btn')) {
                removeRow(e.target.closest('.remove-row-btn'));
            }
        });
    }

    const addRowBtn = document.querySelector('.add-row-btn');
    if (addRowBtn) addRowBtn.addEventListener('click', addNewRow);
}

function validateRow(itemSelect, showAlert = false) {
    if (!itemSelect) return false;
    const row = itemSelect.closest('tr');
    const group = itemSelect.options[itemSelect.selectedIndex]?.dataset.group || '';
    let isValid = true;

    const setError = (input, message) => {
        if (input) {
            const errorSpan = input.nextElementSibling;
            if (errorSpan && errorSpan.classList.contains('text-danger')) errorSpan.textContent = message;
        }
        isValid = false;
    };

    const clearError = (input) => {
        if (input) {
            const errorSpan = input.nextElementSibling;
            if (errorSpan && errorSpan.classList.contains('text-danger')) errorSpan.textContent = '';
        }
    };

    const inputs = {
        pc: row.querySelector('.pc-input'),
        weight: row.querySelector('.weight-input'),
        tunch: row.querySelector('.tunch-input'),
        wastage: row.querySelector('.wastage-input'),
        rate: row.querySelector('.rate-input')
    };

    if (!itemSelect.value) {
        setError(itemSelect, 'Item is required.');
        return false;
    } else {
        clearError(itemSelect);
    }

    switch (group) {
        case 'Gold Jewelry':
            if (!inputs.weight?.value || parseFloat(inputs.weight.value) <= 0) setError(inputs.weight, 'Gross Weight is required.');
            else clearError(inputs.weight);
            if (!inputs.tunch?.value || parseFloat(inputs.tunch.value) <= 0) setError(inputs.tunch, 'Tunch is required.');
            else clearError(inputs.tunch);
            if (!inputs.wastage?.value || parseFloat(inputs.wastage.value) <= 0) setError(inputs.wastage, 'Wastage is required.');
            else clearError(inputs.wastage);
            break;
        case 'PC Gold Jewelry':
            if (!inputs.pc?.value || parseFloat(inputs.pc.value) <= 0) setError(inputs.pc, 'Pc is required.');
            else clearError(inputs.pc);
            if (!inputs.rate?.value || parseFloat(inputs.rate.value) <= 0) setError(inputs.rate, 'Rate is required.');
            else clearError(inputs.rate);
            break;
        case 'PC/Weight Jewelry':
            if (!inputs.pc?.value || parseFloat(inputs.pc.value) <= 0) setError(inputs.pc, 'Pc is required.');
            else clearError(inputs.pc);
            if (!inputs.weight?.value || parseFloat(inputs.weight.value) <= 0) setError(inputs.weight, 'Gross Weight is required.');
            else clearError(inputs.weight);
            if (!inputs.tunch?.value || parseFloat(inputs.tunch.value) <= 0) setError(inputs.tunch, 'Tunch is required.');
            else clearError(inputs.tunch);
            if (!inputs.wastage?.value || parseFloat(inputs.wastage.value) <= 0) setError(inputs.wastage, 'Wastage is required.');
            else clearError(inputs.wastage);
            if (!inputs.rate?.value || parseFloat(inputs.rate.value) <= 0) setError(inputs.rate, 'Rate is required.');
            else clearError(inputs.rate);
            break;
    }

    return isValid;
}

document.addEventListener('DOMContentLoaded', () => {
    setupEventListeners();
    setupFormSubmission('/OpeningStock/AddOpeningStock', '/OpeningStock/DeleteOpeningStock');
});