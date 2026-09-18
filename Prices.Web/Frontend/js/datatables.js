import $ from 'jquery';
import DataTable from 'datatables.net-dt';


$.extend(true, DataTable.defaults, {
    pageLength: 10,
    language: { search: '', searchPlaceholder: 'Pronađi artikl...' }
});


window.DataTable = DataTable; 