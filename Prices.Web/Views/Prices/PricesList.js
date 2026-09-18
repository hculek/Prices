const $table = $('#pricesTable');
let table;

$(function () {
    table = InitTable();

    $table.on('click', '.js-follow', function () {
        followUnfollow(this, $table.data('follow-url'), true);
    });
    $table.on('click', '.js-unfollow', function () {
        followUnfollow(this, $table.data('unfollow-url'), false);
    });
});

function InitTable() {
    return new DataTable('#pricesTable', {
        ajax: $table.data('url'),
        columns: [
            { data: 'barcode', title: 'Barkod' },
            { data: 'product', title: 'Naziv' },
            { data: 'price', title: 'Cijena' },
            { data: 'retailer', title: 'Trgovac' },
            { data: 'retailerunit', title: 'Trgovina' },
            {
                data: 'following', title: 'Praćenje', orderable: false, searchable: false,
                render: (following) => following
                    ? '<button type="button" class="btn btn-sm btn-link js-unfollow" title="Prestani pratiti" aria-label="Prestani pratiti"><i class="bi bi-eye-slash fs-5 text-danger"></i></button>'
                    : '<button type="button" class="btn btn-sm btn-link js-follow" title="Prati" aria-label="Prati"><i class="bi bi-eye fs-5 text-primary"></i></button>'
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

async function followUnfollow(button, url, newState) {
    const barcode = String(table.row(button.closest('tr')).data().barcode);
    console.log(barcode);

    button.disabled = true;

    try {
        await $.ajax({
            url: url,
            type: 'POST',
            data: {barcode: barcode}
        });

        table.rows().every(function () {
            const item = this.data();
            if (String(item.barcode) === barcode) {
                item.following = newState;
                this.invalidate('data');
            }
        });
        table.draw(false);

    } catch (error) {
        button.disabled = false;
        alert('Došlo je do greške. Pokušajte ponovno.');
    }
}