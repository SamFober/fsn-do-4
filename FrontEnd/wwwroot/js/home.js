// Home page functionality

// Handle navbar transparency on scroll
document.addEventListener('DOMContentLoaded', function() {
    const navbar = document.querySelector('.homepage-navbar');
    
    if (navbar) {
        window.addEventListener('scroll', function() {
            if (window.scrollY > 50) {
                navbar.style.backgroundColor = 'rgba(0, 0, 0, 0.95)';
            } else {
                navbar.style.backgroundColor = 'rgba(0, 0, 0, 0.8)';
            }
        });
    }
    
    // Add the is-page-home class to the body element when on home page
    document.body.classList.add('is-page-home');
    
    // Listen for navigation away from the home page
    window.addEventListener('beforeunload', function() {
        // Only runs when leaving the page
        document.body.classList.remove('is-page-home');
    });
});

// Close mobile menu when clicking outside
document.addEventListener('click', function(event) {
    const mobileMenu = document.querySelector('.nav-links.active');
    const menuIcon = document.querySelector('.menu-icon');
    
    if (mobileMenu && !mobileMenu.contains(event.target) && !menuIcon.contains(event.target)) {
        if (window.DotNet) {
            DotNet.invokeMethodAsync('FrontEnd', 'CloseMobileMenu');
        }
    }
}); 