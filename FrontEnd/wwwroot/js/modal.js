// Modal handling
function showModal(modalId) {
    var modal = new bootstrap.Modal(document.getElementById(modalId));
    modal.show();
}

function hideModal(modalId) {
    var modalElement = document.getElementById(modalId);
    var modal = bootstrap.Modal.getInstance(modalElement);
    if (modal) {
        modal.hide();
    }
}

// Scroll tracking for progress bar
let dotNetReference;

function initScrollTracking() {
    dotNetReference = DotNet.createJSObjectReference(document);
    setupScrollListener();
}

function setupScrollListener() {
    document.addEventListener('scroll', updateProgressBasedOnScroll);
    // Initial check on page load
    updateProgressBasedOnScroll();
}

function updateProgressBasedOnScroll() {
    if (!dotNetReference) return;
    
    const container = document.getElementById('cinemaContainer');
    if (!container) return;
    
    const seatMap = document.querySelector('.seat-map');
    const ticketSection = document.getElementById('ticketTypeSection');
    
    if (!seatMap) return;
    
    const scrollPosition = window.scrollY + (window.innerHeight / 2);
    
    // Step 1: Seat selection
    if (!ticketSection || scrollPosition < ticketSection.offsetTop) {
        DotNet.invokeMethodAsync('FrontEnd', 'UpdateProgressStep', 1);
    } 
    // Step 2: Payment (ticket type selection)
    else if (ticketSection && scrollPosition >= ticketSection.offsetTop) {
        DotNet.invokeMethodAsync('FrontEnd', 'UpdateProgressStep', 2);
    }
}

// Expose functions to window object
window.showModal = showModal;
window.hideModal = hideModal;
window.initScrollTracking = initScrollTracking;
window.setupScrollListener = setupScrollListener; 