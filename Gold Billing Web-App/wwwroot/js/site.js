// site.js
function exportToPDF() {
    const billNoElement = document.getElementById('BillNo');
    const billNo = billNoElement ? billNoElement.value : 'NewTransaction';

    // Get the transaction table data
    const headers = Array.from(document.querySelectorAll('#transactionTable thead th:not(:last-child)'))
        .map(th => th.innerText);
    const data = Array.from(document.querySelectorAll('#transactionTable tbody tr'))
        .map(row => {
            return Array.from(row.querySelectorAll('td:not(:last-child)'))
                .map(td => {
                    let input = td.querySelector('input:not([type=hidden])') || td.querySelector('select');
                    return input ? (input.value || '') : td.innerText;
                });
        });

    // Get the previous balance date
    const prevDateElement = document.getElementById('prevDate');
    const prevDate = prevDateElement ? prevDateElement.innerText : 'N/A';

    // Get totals and balances
    const totals = [
        ['Total', '', document.getElementById('totalGrWt').value, document.getElementById('totalLess').value, document.getElementById('totalNetWt').value, '', '', '', '', document.getElementById('totalFine').value, document.getElementById('totalAmount').value],
        [`Previous Balance Date: ${prevDate}`, '', '', '', '', '', '', '', '', '', ''], // Add previous balance date row
        ['Previous Balance', '', '', '', '', '', '', '', '', document.getElementById('prevFine').value, document.getElementById('prevAmount').value],
        ['Current Transaction', '', '', '', '', '', '', '', '', document.getElementById('currentFine').value, document.getElementById('currentAmount').value],
        ['Total Balance', '', '', '', '', '', '', '', '', document.getElementById('totalBalanceFine').value, document.getElementById('totalBalanceAmount').value]
    ];

    const { jsPDF } = window.jspdf;
    const doc = new jsPDF({
        orientation: 'portrait',
        unit: 'mm',
        format: 'a4'
    });

    // Add title
    doc.setFontSize(16);
    doc.text(`Transaction - ${billNo}`, 10, 10);

    // Add transaction table
    doc.autoTable({
        head: [headers],
        body: data,
        startY: 20,
        theme: 'grid',
        styles: { fontSize: 8 },
        headStyles: { fillColor: [100, 100, 100] },
        margin: { top: 20 }
    });

    // Add totals and balances with custom styling for the previous balance date row
    doc.autoTable({
        body: totals,
        startY: doc.lastAutoTable.finalY + 10,
        theme: 'grid',
        styles: { fontSize: 8 },
        headStyles: { fillColor: [100, 100, 100] },
        didParseCell: function (data) {
            if (data.row.index === 1 && data.column.index === 0) { // Previous Balance Date row
                data.cell.styles.textColor = [255, 0, 0]; // Red color
                data.cell.styles.fontSize = 7; // Smaller font
            }
        }
    });

    doc.save(`Transaction_${billNo}.pdf`);
}