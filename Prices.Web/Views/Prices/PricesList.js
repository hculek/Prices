$(document).ready(function () {
    InitTable();
});


function InitTable() {
    new DataTable('#pricesTable', {
        ajax: $('#pricesTable').data('url'),
        columns: [
            { data: 'barcode', title: 'Barkod' },
            { data: 'product', title: 'Naziv' },
            { data: 'price', title: 'Cijena' },
            { data: 'retailer', title: 'Trgovac' },
            { data: 'retailerunit', title: 'Trgovina' }
        ],
        layout: {
            topStart: 'pageLength',
            topEnd: 'search',
            bottomStart: 'info',
            bottomEnd: 'paging'
        }
    });
}