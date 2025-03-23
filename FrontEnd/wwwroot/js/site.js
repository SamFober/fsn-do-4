// Add a function to scroll to an element by ID
window.scrollToElement = function (elementId) {
    const element = document.getElementById(elementId);
    if (element) {
        element.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
}; 

// Add a function to navigate back with fallback
window.navigateBack = function() {
    // Check if there's at least one entry in the history (besides the current page)
    if (window.history.length > 1) {
        window.history.back();
    } else {
        // If no history, check if we came from schedule or movie detail
        // by looking at the referrer URL
        const referrer = document.referrer;
        
        if (referrer && referrer.includes('/schedule')) {
            window.location.href = '/schedule';
        } else if (referrer && referrer.includes('/film/')) {
            // Extract the movie ID from the referrer if possible
            const movieIdMatch = referrer.match(/\/film\/(\d+)/);
            if (movieIdMatch && movieIdMatch[1]) {
                window.location.href = `/film/${movieIdMatch[1]}`;
            } else {
                window.location.href = '/schedule';
            }
        } else {
            // Default fallback to schedule page
            window.location.href = '/schedule';
        }
    }
};

// Ensure the function is properly registered in the global scope
// This is a more reliable way to expose functions to Blazor
window.cinemaFunctions = {
    scrollToElement: function(elementId) {
        const element = document.getElementById(elementId);
        if (element) {
            element.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    },
    navigateBack: function() {
        window.navigateBack();
    }
}; 