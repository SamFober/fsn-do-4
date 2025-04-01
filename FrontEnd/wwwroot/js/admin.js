// Initialize modal close buttons
function initModalCloseButtons() {
    const closeButtons = document.querySelectorAll('.modal-close, .admin-modal-close');
    closeButtons.forEach(button => {
        if (button.childElementCount === 0) {
            // Add close icon and text if not already present
            button.innerHTML = '<i class="fas fa-times"></i><span>Close</span>';
        } else if (button.textContent.trim() === '×' || button.textContent.trim() === '') {
            // Replace × with icon and text
            button.innerHTML = '<i class="fas fa-times"></i><span>Close</span>';
        }
    });
}

// Add to existing window load event or create one
document.addEventListener('DOMContentLoaded', function() {
    // Call once on page load
    initModalCloseButtons();
    
    // Set up a mutation observer to handle dynamically added modals
    const observer = new MutationObserver(function(mutations) {
        mutations.forEach(function(mutation) {
            if (mutation.addedNodes.length) {
                initModalCloseButtons();
            }
        });
    });
    
    // Start observing the document body for added nodes
    observer.observe(document.body, { childList: true, subtree: true });
    
    // Initialize clicking outside modals to close them
    document.addEventListener('click', function(event) {
        if (event.target.classList.contains('modal-overlay')) {
            const modalContainers = document.querySelectorAll('.modal-container');
            modalContainers.forEach(container => {
                const parent = container.parentElement;
                if (parent && parent.classList.contains('modal-overlay')) {
                    closeModal(parent, container);
                }
            });
        }
    });
});

// Close modal function - to be called by the application code
function closeModal(overlay, container) {
    if (overlay) overlay.style.display = 'none';
    if (container) container.style.display = 'none';
    document.body.style.overflow = '';
}

// Function to switch between different modals within the same overlay
function initModalSwitching() {
    const modalTriggers = document.querySelectorAll('[data-modal-target]');
    modalTriggers.forEach(trigger => {
        trigger.addEventListener('click', function(e) {
            e.preventDefault();
            
            // Get the current modal container
            const currentModal = this.closest('.modal-container');
            
            // Get the target modal ID
            const targetId = this.getAttribute('data-modal-target');
            const targetModal = document.getElementById(targetId);
            
            if (currentModal && targetModal) {
                // Hide current modal
                currentModal.style.display = 'none';
                
                // Show target modal
                targetModal.style.display = 'block';
                
                // Fix z-index if needed
                fixModalZIndex();
            }
        });
    });
}

// Fix z-index issues with multiple modals
function fixModalZIndex() {
    const modals = document.querySelectorAll('.modal-container');
    const overlays = document.querySelectorAll('.modal-overlay');
    
    // Set base z-index
    let baseZIndex = 2000;
    
    // Set overlay z-indices
    overlays.forEach((overlay, index) => {
        if (overlay.style.display !== 'none') {
            overlay.style.zIndex = (baseZIndex + (index * 10)).toString();
        }
    });
    
    // Set modal container z-indices
    modals.forEach((modal, index) => {
        if (modal.style.display !== 'none') {
            modal.style.zIndex = (baseZIndex + 1 + (index * 10)).toString();
        }
    });
} 