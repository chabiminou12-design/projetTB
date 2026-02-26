$(document).ready(function () {
    function loedPartialView(viewUrl) {
        $.ajax({
            url: viewUrl,
            type: 'GET',
            success: function (result) {
                $('#mainContent').html(result);
            },
            Error: function () {
                alert('Une erreur est survenue lors du chargement de la vue.');
            } 
        });
    }
    $('a[data-view]').click(function (e) {
        e.preventDefault();
        var viewUrl = $(this).data('view');
        loadPartialView(viewUrl);
    });
});