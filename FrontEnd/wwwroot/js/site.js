// Add a function to scroll to an element by ID
window.scrollToElement = function (elementId) {
    const element = document.getElementById(elementId);
    if (element) {
        element.scrollIntoView({ behavior: 'smooth', block: 'start' });
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
    }
}; 