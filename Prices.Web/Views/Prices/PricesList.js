$(document).ready(function () {
    InitTable();
});


function InitTable() {
    new DataTable('#pricesTable', {
        ajax: $('#pricesTable').data('url'),
        columns: [
            { data: 'barcode', title: 'Barkod' },
            { data: 'product', title: 'Naziv' },
            { data: 'price', title: 'Cijena', orderable: true},
            { data: 'retailer', title: 'Trgovac' },
            { data: 'retailerunit', title: 'Trgovina' },
            {
                data: 'Following', title: 'Praćenje', orderable: false, searchable: false,
                render: (Following) => Following
                    ? '<button type="button" class="btn btn-sm btn-link"><i class="bi bi-eye-slash me-2"></i></button>'
                    : '<button type="button" class="btn btn-sm btn-link"><i class="bi bi-eye me-2"></i></button>'
            }
        ],
        order: [[2, 'asc']],
        layout: {
            topStart: 'search', 
            topEnd: null,
            bottomStart: null,
            bottomEnd: ['paging', 'pageLength']
        }
    });
}