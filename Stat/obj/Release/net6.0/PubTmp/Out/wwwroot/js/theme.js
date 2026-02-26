// wwwroot/js/theme.js
document.addEventListener('DOMContentLoaded', function () {

    // --- Sidebar Toggle Logic ---
    const sidebarToggle = document.getElementById('sidebarToggle');
    if (sidebarToggle) {
        sidebarToggle.addEventListener('click', function (event) {
            event.preventDefault();
            document.getElementById('app-wrapper').classList.toggle('sidebar-toggled');
        });
    }

    // --- NEW: Active Sidebar Link Logic ---
    // This script automatically finds the current page in the sidebar and highlights it.
    const currentPath = window.location.pathname;
    const sidebarLinks = document.querySelectorAll('#app-sidebar .nav-link');

    sidebarLinks.forEach(link => {
        // If the link's href matches the current page path, add 'active' class
        if (link.getAttribute('href') === currentPath) {
            link.classList.add('active');

            // If it's inside a collapsed menu, expand the menu
            const collapseParent = link.closest('.collapse');
            if (collapseParent) {
                new bootstrap.Collapse(collapseParent, {
                    toggle: true
                });
            }
        }
    });
});