// transaction.js

// #region Initialization and Event Listeners

document.addEventListener('DOMContentLoaded', function () {
    updateTotals();
    fetchPreviousBalance();
    syncAccountIds();
    recalculateAllRows();
});

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
            fetch(addTransactionUrl, {
                method: 'POST',
                body: new FormData(this),
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

function setupEventListeners() {
    document.getElementById('accountSelect').addEventListener('change', function () {
        syncAccountIds();
        fetchPreviousBalance();
        recalculateAllRows();
    });

    document.querySelectorAll('.item-select').forEach(select => {
        select.addEventListener('change', function () {
            validateRow(this);
            recalculateAllRows();
        });
    });

    document.getElementById('transactionTable').addEventListener('change', function (e) {
        if (e.target.classList.contains('weight-input') || e.target.classList.contains('less-input')) {
            calculateNetWeight(e.target);
        }
        if (e.target.classList.contains('tunch-input') || e.target.classList.contains('wastage-input')) {
            calculateTW(e.target);
        }
        if (e.target.classList.contains('pc-input') || e.target.classList.contains('rate-input')) {
            calculateAmount(e.target);
        }
    });
}

// #endregion

// #region Row Calculations

function calculateNetWeight(element) {
    let row = element.closest('tr');
    let grossWeight = parseFloat(row.querySelector('.weight-input').value) || 0;
    let less = parseFloat(row.querySelector('.less-input').value) || 0;
    let netWeight = Math.max(0, grossWeight - less);
    row.querySelector('.netwt-input').value = netWeight.toFixed(2);
    calculateFineAndAmount(row);
}

function calculateTW(element) {
    let row = element.closest('tr');
    let tunch = parseFloat(row.querySelector('.tunch-input').value) || 0;
    let wastage = parseFloat(row.querySelector('.wastage-input').value) || 0;
    let tw = tunch + wastage;
    row.querySelector('.tw-input').value = tw.toFixed(2);
    calculateFineAndAmount(row);
}

function calculateFineAndAmount(row) {
    let netWeight = parseFloat(row.querySelector('.netwt-input').value) || 0;
    let tw = parseFloat(row.querySelector('.tw-input').value) || 0;
    let rate = parseFloat(row.querySelector('.rate-input').value) || 0;

    let fine = netWeight * (tw / 100);
    let amount = fine * rate;

    row.querySelector('.fine-input').value = fine.toFixed(2);
    row.querySelector('.amount-input').value = amount.toFixed(2);
    row.querySelector('.hidden-amount-input').value = amount.toFixed(2);
}

function calculateAmount(element) {
    let row = element.closest('tr');
    let groupName = row.querySelector('.item-select').selectedOptions[0]?.getAttribute('data-group') || '';
    let pc = parseFloat(row.querySelector('.pc-input').value) || 0;
    let rate = parseFloat(row.querySelector('.rate-input').value) || 0;

    if (groupName === 'PC Gold Jewelry') {
        let amount = pc * rate;
        row.querySelector('.amount-input').value = amount.toFixed(2);
        row.querySelector('.hidden-amount-input').value = amount.toFixed(2);
        row.querySelector('.fine-input').value = '0.00';
        row.querySelector('.netwt-input').value = '0.00';
        row.querySelector('.tw-input').value = '0.00';
    } else {
        calculateFineAndAmount(row);
    }
}

function recalculateAllRows() {
    document.querySelectorAll('#transactionTable tbody tr').forEach(row => {
        let groupName = row.querySelector('.item-select').selectedOptions[0]?.getAttribute('data-group') || '';
        if (groupName === 'Gold Jewelry' || groupName === 'PC/Weight Jewelry') {
            calculateNetWeight(row.querySelector('.weight-input'));
        } else if (groupName === 'PC Gold Jewelry') {
            calculateAmount(row.querySelector('.pc-input'));
        }
    });
    updateTotals();
}

// #endregion

// #region Row Management

function addNewRow(transactionType) {
    let table = document.getElementById('transactionTable').querySelector('tbody');
    let rowCount = table.getElementsByTagName('tr').length;
    let newRow = table.rows[0].cloneNode(true);

    newRow.querySelectorAll("input:not([readonly]), select").forEach(input => {
        let baseName = input.name.includes('Items') ? input.name.split('.')[1] : input.name;
        input.name = `Items[${rowCount}].${baseName}`;
        input.id = `Items_${rowCount}__${baseName}`;
        if (input.type !== 'hidden' && input.tagName !== 'SELECT') input.value = '';
        if (baseName === 'AccountId') input.value = document.getElementById('accountSelect').value || '';
        if (baseName === 'TransactionType') input.value = transactionType;
    });

    newRow.querySelectorAll("input[readonly]").forEach(input => {
        let baseName = input.name.includes('Items') ? input.name.split('.')[1] : input.name;
        input.name = `Items[${rowCount}].${baseName}`;
        input.id = `Items_${rowCount}__${baseName}`;
        input.value = '0.00';
    });

    newRow.querySelectorAll("input[type='hidden'].hidden-amount-input").forEach(input => {
        input.name = `Items[${rowCount}].Amount`;
        input.id = `Items_${rowCount}__Amount_hidden`;
        input.value = '0.00';
    });

    newRow.querySelectorAll("span.text-danger").forEach(span => {
        let baseFor = span.getAttribute('data-valmsg-for').split('.')[1];
        span.setAttribute('data-valmsg-for', `Items[${rowCount}].${baseFor}`);
        span.innerHTML = "";
    });

    let actionCell = newRow.querySelector('td:last-child');
    actionCell.innerHTML = '<button type="button" class="btn btn-danger btn-sm" onclick="removeRow(this)">Remove</button>';

    table.appendChild(newRow);
    $.validator.unobtrusive.parse(newRow);
    recalculateAllRows();
}

function removeRow(button) {
    let table = document.getElementById('transactionTable').querySelector('tbody');
    if (table.rows.length > 1) {
        button.closest('tr').remove();
        recalculateAllRows();
    } else {
        Swal.fire({
            icon: 'warning',
            title: 'Cannot Remove',
            text: 'At least one row is required.',
        });
    }
}

// #endregion

// #region Totals and Balances

function updateTotals() {
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

    let prevFine = parseFloat(document.getElementById('prevFine').value) || 0;
    let prevAmount = parseFloat(document.getElementById('prevAmount').value) || 0;
    let currentFine = (window.transactionType === 'Sale' || window.transactionType === 'PurchaseReturn') ? -totalFine : totalFine;
    let currentAmount = (window.transactionType === 'Sale' || window.transactionType === 'PurchaseReturn') ? -totalAmount : totalAmount;

    document.getElementById('currentFine').value = currentFine.toFixed(2);
    document.getElementById('currentAmount').value = currentAmount.toFixed(2);
    document.getElementById('totalBalanceFine').value = (prevFine + currentFine).toFixed(2);
    document.getElementById('totalBalanceAmount').value = (prevAmount + currentAmount).toFixed(2);
}

function fetchPreviousBalance() {
    let accountId = document.getElementById('accountSelect').value;
    const prevFineInput = document.getElementById('prevFine');
    const prevAmountInput = document.getElementById('prevAmount');
    const prevDateSpan = document.getElementById('prevDate');

    if (!accountId) {
        prevFineInput.value = "0.00";
        prevAmountInput.value = "0.00";
        prevDateSpan.innerText = "";
        prevFineInput.style.color = ""; // Reset color
        prevAmountInput.style.color = ""; // Reset color
        updateTotals();
        return;
    }

    fetch(`/Transaction/GetPreviousBalance?accountId=${accountId}`)
        .then(response => response.json())
        .then(data => {
            prevFineInput.value = data.fine.toFixed(2);
            prevAmountInput.value = data.amount.toFixed(2);
            prevDateSpan.innerText = data.date ? new Date(data.date).toISOString().split('T')[0] : "";
            // Apply red font styling to previous balance
            prevFineInput.style.color = "red";
            prevAmountInput.style.color = "red";
            updateTotals();
        })
        .catch(error => {
            console.error('Error fetching previous balance:', error);
            prevFineInput.value = "0.00";
            prevAmountInput.value = "0.00";
            prevDateSpan.innerText = "";
            prevFineInput.style.color = ""; // Reset color on error
            prevAmountInput.style.color = ""; // Reset color on error
            updateTotals();
        });
}
// #endregion

// #region Print and Export

function getPrintContent(billNoElement, transactionForm, stylesheets) {
    return `
        <html>
            <head>
                <title>Transaction - ${billNoElement.value}</title>
                <style>
                    ${stylesheets}
                    body { font-family: Arial, sans-serif; margin: 20px; }
                    .form-control { border: 1px solid #000; }
                    .table { width: 100%; border-collapse: collapse; }
                    .table th, .table td { border: 1px solid #000; padding: 8px; }
                    .text-danger, button { display: none; }
                </style>
            </head>
            <body>
                <h2>Transaction - ${billNoElement.value}</h2>
                ${transactionForm.outerHTML}
            </body>
        </html>
    `;
}
// site.js
function exportToPDF() {
    const billNoElement = document.getElementById('BillNo');
    const transactionForm = document.getElementById('transactionForm');
    if (!billNoElement || !transactionForm) {
        throw new Error('Required elements (BillNo or transactionForm) not found.');
    }

    const stylesheets = Array.from(document.styleSheets)
        .map(sheet => {
            try {
                return Array.from(sheet.cssRules)
                    .map(rule => rule.cssText)
                    .join('\n');
            } catch (e) {
                return '';
            }
        })
        .filter(style => style)
        .join('\n');

    const content = `
        <html>
            <head>
                <title>Transaction - ${billNoElement.value}</title>
                <style>
                    ${stylesheets}
                    body { font-family: Arial, sans-serif; margin: 20px; }
                    .form-control { border: 1px solid #000; }
                    .table { width: 100%; border-collapse: collapse; }
                    .table th, .table td { border: 1px solid #000; padding: 8px; }
                    .text-danger, button { display: none; }
                </style>
            </head>
            <body>
                <h2>Transaction - ${billNoElement.value}</h2>
                ${transactionForm.outerHTML}
            </body>
        </html>
    `;

    const { jsPDF } = window.jspdf;
    const doc = new jsPDF();
    doc.html(content, {
        callback: function (doc) {
            doc.save(`Transaction_${billNoElement.value}.pdf`);
        },
        x: 10,
        y: 10,
        html2canvas: { scale: 0.5 } // Adjust scale for better fit
    });
}

function exportToPDF() {
    try {
        window.exportToPDF();
        Swal.fire({
            icon: 'success',
            title: 'Success',
            text: 'Transaction exported to PDF successfully!',
            timer: 1500,
            showConfirmButton: false
        });
    } catch (error) {
        console.error('Error during PDF export:', error);
        Swal.fire({
            icon: 'error',
            title: 'Error',
            text: 'Failed to export the transaction to PDF: ' + error.message
        });
    }
}

function printTransaction() {
    try {
        const billNoElement = document.getElementById('BillNo');
        const transactionForm = document.getElementById('transactionForm');
        if (!billNoElement || !transactionForm) {
            throw new Error('Required elements (BillNo or transactionForm) not found.');
        }

        const printWindow = window.open('', '_blank');
        if (!printWindow) {
            throw new Error('Failed to open print window. Please allow pop-ups.');
        }

        const stylesheets = Array.from(document.styleSheets)
            .map(sheet => {
                try {
                    return Array.from(sheet.cssRules)
                        .map(rule => rule.cssText)
                        .join('\n');
                } catch (e) {
                    return '';
                }
            })
            .filter(style => style)
            .join('\n');

        const content = getPrintContent(billNoElement, transactionForm, stylesheets);

        printWindow.document.write(content);
        printWindow.document.close();

        const script = printWindow.document.createElement('script');
        script.textContent = `
            window.onload = function() {
                window.print();
                setTimeout(() => window.close(), 100);
            };
        `;
        printWindow.document.body.appendChild(script);

        Swal.fire({
            icon: 'success',
            title: 'Success',
            text: 'Transaction sent to printer!',
            timer: 1500,
            showConfirmButton: false
        });
    } catch (error) {
        console.error('Error during printing:', error);
        Swal.fire({
            icon: 'error',
            title: 'Error',
            text: 'Failed to print the transaction: ' + error.message
        });
    }
}

function exportToExcel() {
    let headers = Array.from(document.querySelectorAll('#transactionTable thead th:not(:last-child)')).map(th => th.innerText);
    let data = Array.from(document.querySelectorAll('#transactionTable tbody tr')).map(row => {
        return Array.from(row.querySelectorAll('td:not(:last-child)')).map(td => {
            let input = td.querySelector('input:not([type=hidden])') || td.querySelector('select');
            return input ? (input.value || '') : td.innerText;
        });
    });

    data.push(['Total', '', document.getElementById('totalGrWt').value, document.getElementById('totalLess').value, document.getElementById('totalNetWt').value, '', '', '', '', document.getElementById('totalFine').value, document.getElementById('totalAmount').value]);
    data.push(['Previous Balance', '', '', '', '', '', '', '', '', document.getElementById('prevFine').value, document.getElementById('prevAmount').value]);
    data.push(['Current Transaction', '', '', '', '', '', '', '', '', document.getElementById('currentFine').value, document.getElementById('currentAmount').value]);
    data.push(['Total Balance', '', '', '', '', '', '', '', '', document.getElementById('totalBalanceFine').value, document.getElementById('totalBalanceAmount').value]);

    let csvContent = "data:text/csv;charset=utf-8," + headers.join(",") + "\n" + data.map(row => row.join(",")).join("\n");
    let encodedUri = encodeURI(csvContent);
    let link = document.createElement("a");
    link.setAttribute("href", encodedUri);
    link.setAttribute("download", `Transaction_${document.getElementById('BillNo').value}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);

    Swal.fire({
        icon: 'success',
        title: 'Success',
        text: 'Transaction exported to Excel successfully!',
        timer: 1500,
        showConfirmButton: false
    });
}

// #endregion

// #region Transaction Management

function syncAccountIds() {
    let accountId = document.getElementById('accountSelect').value;
    document.querySelectorAll('.account-id-input').forEach(input => input.value = accountId || '');
}

function validateRow(element, isSubmit = false) {
    let row = element.closest('tr');
    let groupName = row.querySelector('.item-select').selectedOptions[0]?.getAttribute('data-group') || '';
    let accountId = document.getElementById('accountSelect').value;
    let pc = parseFloat(row.querySelector('.pc-input').value) || 0;
    let weight = parseFloat(row.querySelector('.weight-input').value) || 0;
    let wastage = parseFloat(row.querySelector('.wastage-input').value) || 0;
    let rate = parseFloat(row.querySelector('.rate-input').value) || 0;

    row.querySelectorAll('.text-danger').forEach(span => span.innerHTML = '');
    document.getElementById('account-error').innerHTML = '';

    const allFields = ['pc-input', 'weight-input', 'less-input', 'tunch-input', 'wastage-input', 'rate-input', 'amount-input'];
    const alwaysEnabledFields = ['item-select'];

    if (groupName === 'Gold Jewelry') {
        allFields.forEach(className => row.querySelector(`.${className}`).disabled = (className === 'pc-input' || className === 'amount-input'));
        alwaysEnabledFields.forEach(className => row.querySelector(`.${className}`).disabled = false);
        row.querySelector('.pc-input').value = '';
    } else if (groupName === 'PC Gold Jewelry') {
        allFields.forEach(className => row.querySelector(`.${className}`).disabled = (className !== 'pc-input' && className !== 'rate-input'));
        alwaysEnabledFields.forEach(className => row.querySelector(`.${className}`).disabled = false);
        calculateAmount(row.querySelector('.pc-input'));
    } else if (groupName === 'PC/Weight Jewelry') {
        allFields.forEach(className => row.querySelector(`.${className}`).disabled = false);
        alwaysEnabledFields.forEach(className => row.querySelector(`.${className}`).disabled = false);
        calculateNetWeight(row.querySelector('.weight-input'));
    } else {
        allFields.forEach(className => row.querySelector(`.${className}`).disabled = false);
        alwaysEnabledFields.forEach(className => row.querySelector(`.${className}`).disabled = false);
    }

    let isValid = true;
    if (isSubmit) {
        if (!accountId) {
            document.getElementById('account-error').innerHTML = 'Account is required.';
            isValid = false;
        }
        if (!groupName) {
            row.querySelector('.item-select + .text-danger').innerHTML = 'Item is required.';
            isValid = false;
        } else {
            if (groupName === 'Gold Jewelry') {
                if (weight <= 0) row.querySelector('.weight-input + .text-danger').innerHTML = 'Gross Weight is required.', isValid = false;
                if (wastage <= 0) row.querySelector('.wastage-input + .text-danger').innerHTML = 'Wastage is required.', isValid = false;
            } else if (groupName === 'PC Gold Jewelry') {
                if (pc <= 0) row.querySelector('.pc-input + .text-danger').innerHTML = 'Pc is required.', isValid = false;
                if (rate <= 0) row.querySelector('.rate-input + .text-danger').innerHTML = 'Rate is required.', isValid = false;
            } else if (groupName === 'PC/Weight Jewelry') {
                if (pc <= 0) row.querySelector('.pc-input + .text-danger').innerHTML = 'Pc is required.', isValid = false;
                if (weight <= 0) row.querySelector('.weight-input + .text-danger').innerHTML = 'Gross Weight is required.', isValid = false;
                if (wastage <= 0) row.querySelector('.wastage-input + .text-danger').innerHTML = 'Wastage is required.', isValid = false;
                if (rate <= 0) row.querySelector('.rate-input + .text-danger').innerHTML = 'Rate is required.', isValid = false;
            }
        }
    }
    return isValid;
}

function deleteTransaction(billNo) {
    Swal.fire({
        title: 'Are you sure?',
        text: `You are about to delete transaction ${billNo}. This action cannot be undone.`,
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#d33',
        cancelButtonColor: '#3085d6',
        confirmButtonText: 'Yes, delete it!'
    }).then((result) => {
        if (result.isConfirmed) {
            fetch(`/Transaction/DeleteTransaction?billNo=${billNo}`, {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                }
            })
                .then(response => response.json())
                .then(data => {
                    if (data.success) {
                        Swal.fire({
                            icon: 'success',
                            title: 'Deleted!',
                            text: data.message || 'Transaction deleted successfully!',
                            timer: 1500,
                            showConfirmButton: false
                        }).then(() => {
                            window.location.href = data.redirectUrl;
                        });
                    } else {
                        Swal.fire({
                            icon: 'error',
                            title: 'Error',
                            text: data.error || 'Failed to delete the transaction.'
                        });
                    }
                })
                .catch(error => {
                    console.error('Error during deletion:', error);
                    Swal.fire({
                        icon: 'error',
                        title: 'Error',
                        text: 'An error occurred while deleting the transaction.'
                    });
                });
        }
    });
}

// #endregion