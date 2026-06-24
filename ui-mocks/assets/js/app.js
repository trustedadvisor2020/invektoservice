document.addEventListener('DOMContentLoaded', () => {
    // Determine current path to highlight active nav item
    const path = window.location.pathname;
    const navItems = document.querySelectorAll('.nav-item');
    
    navItems.forEach(item => {
        const href = item.getAttribute('href');
        if (href && path.includes(href.replace('./', '').replace('../', ''))) {
            item.classList.add('active');
        } else {
            item.classList.remove('active');
        }
    });

    // Optional: Add basic click ripples or interactivity here
    console.log("Chatinbox UI Mocks Loaded - Premium UX initialized.");
});
