// Initialize modals on page load
document.addEventListener('DOMContentLoaded', function() {
    // Hide all modals on page load
    var modals = document.querySelectorAll('.modal, .overlay-modal');
    modals.forEach(function(modal) {
        modal.style.display = 'none';
        modal.classList.remove('show');
    });

    // Remove any existing modal backdrops
    var backdrops = document.querySelectorAll('.modal-backdrop');
    backdrops.forEach(function(backdrop) {
        backdrop.remove();
    });

    // Make sure the body doesn't have any leftover modal classes
    document.body.classList.remove('modal-open');
    document.body.style.overflow = '';
    document.body.style.paddingRight = '';
});

// Function to show a modal
function showModal(modalId) {
    var modal = document.getElementById(modalId);
    if (!modal) return;
    
    // Clear any existing backdrops
    clearAllBackdrops();
    
    // Check if this is a seat map modal (not just a seating option modal)
    var isSeatMapModal = modalId.toLowerCase().includes('seatmap') || 
                         modal.classList.contains('seat-map-modal') ||
                         (modal.querySelector('.seat-map') !== null);
    
    // Apply the appropriate class for seat map modals
    if (isSeatMapModal) {
        modal.classList.add('seat-map-modal');
    } else {
        modal.classList.remove('seat-map-modal');
    }
    
    // Add the modal backdrop for non-seat map modals
    if (!isSeatMapModal) {
        var backdrop = document.createElement('div');
        backdrop.className = 'modal-backdrop show';
        document.body.appendChild(backdrop);
    }
    
    // Show the modal
    modal.style.display = 'flex';
    modal.classList.add('show');
    
    // Prevent body scrolling
    document.body.classList.add('modal-open');
    document.body.style.overflow = 'hidden';
    
    // Set attributes to prevent Bootstrap from auto-closing
    modal.setAttribute('data-backdrop', 'static');
    modal.setAttribute('data-keyboard', 'false');
    
    // Add event listener to prevent closing when clicking outside (except for close buttons)
    modal.addEventListener('click', function(event) {
        if (event.target === modal) {
            // Only prevent default, don't close the modal
            event.preventDefault();
            event.stopPropagation();
        }
    });
    
    // Get all close buttons in this modal
    var closeButtons = modal.querySelectorAll('[data-dismiss="modal"], .close, .btn-close, .close-modal');
    closeButtons.forEach(function(button) {
        button.addEventListener('click', function() {
            hideModal(modalId);
        });
    });
    
    // Disable the ESC key from closing the modal
    document.addEventListener('keydown', function(event) {
        if (event.key === 'Escape') {
            event.preventDefault();
            event.stopPropagation();
            return false;
        }
    });
    
    // Initialize radio options in the modal if they exist
    initializeRadioOptions(modal);
}

// Function to clear all backdrops
function clearAllBackdrops() {
    var backdrops = document.querySelectorAll('.modal-backdrop');
    backdrops.forEach(function(backdrop) {
        backdrop.remove();
    });
}

// Initialize radio options in modals
function initializeRadioOptions(modal) {
    var radioOptions = modal.querySelectorAll('.cinema-radio-option');
    
    if (radioOptions.length > 0) {
        // First, remove selected class from all options
        radioOptions.forEach(function(option) {
            option.classList.remove('selected');
            
            // Make sure content has no border
            var content = option.querySelector('.cinema-radio-content');
            if (content) {
                content.style.border = 'none';
            }
        });
        
        // Initialize each option
        radioOptions.forEach(function(option) {
            var radio = option.querySelector('input[type="radio"]');
            
            // Set initial state - only apply to parent
            if (radio && radio.checked) {
                option.classList.add('selected');
            }
            
            // Add click event to the entire option
            option.addEventListener('click', function(e) {
                // Remove selected class from all options
                radioOptions.forEach(function(opt) {
                    opt.classList.remove('selected');
                });
                
                // Check the radio and add selected class only to parent
                if (radio) {
                    radio.checked = true;
                    option.classList.add('selected');
                    
                    // Trigger change event so Blazor knows about it
                    var event = new Event('change', { bubbles: true });
                    radio.dispatchEvent(event);
                }
            });
            
            // Also add change event to the radio input itself
            if (radio) {
                radio.addEventListener('change', function() {
                    // Remove selected class from all options
                    radioOptions.forEach(function(opt) {
                        opt.classList.remove('selected');
                    });
                    
                    // Add selected class only to parent if this radio is checked
                    if (radio.checked) {
                        option.classList.add('selected');
                    }
                });
            }
        });
    }
}

// Function to hide a modal
function hideModal(modalId) {
    var modal = document.getElementById(modalId);
    if (!modal) return;
    
    modal.style.display = 'none';
    modal.classList.remove('show');
    
    // Remove backdrop for this modal
    var backdrops = document.querySelectorAll('.modal-backdrop');
    backdrops.forEach(function(backdrop) {
        backdrop.remove();
    });
    
    // Check if there are any other open modals
    var openModals = document.querySelectorAll('.modal.show, .overlay-modal.show');
    if (openModals.length === 0) {
        // Only remove body classes if no other modals are open
        document.body.classList.remove('modal-open');
        document.body.style.overflow = '';
        document.body.style.paddingRight = '';
    }
}

// Seat selection progress bar (if exists)
var progressBar = document.querySelector('.progress-bar');
var progressStep = 0;
var totalSteps = 2; // Seat selection and payment

// Update progress based on scroll position
window.addEventListener('scroll', function() {
    if (!progressBar) return;
    
    var scrollTop = window.pageYOffset || document.documentElement.scrollTop;
    var docHeight = document.documentElement.scrollHeight - document.documentElement.clientHeight;
    var scrollPercent = (scrollTop / docHeight) * 100;
    
    // Map scroll percentage to our steps
    if (scrollPercent < 50) {
        progressStep = 0; // Seat selection
    } else {
        progressStep = 1; // Payment
    }
    
    // Update progress bar width
    var width = ((progressStep + 1) / totalSteps) * 100;
    progressBar.style.width = width + '%';
    
    // Update step indicators
    updateStepIndicators(progressStep);
});

// Update step indicators
function updateStepIndicators(currentStep) {
    var stepIndicators = document.querySelectorAll('.step-indicator');
    if (stepIndicators.length === 0) return;
    
    stepIndicators.forEach(function(indicator, index) {
        if (index < currentStep) {
            indicator.classList.add('completed');
            indicator.classList.remove('active');
        } else if (index === currentStep) {
            indicator.classList.add('active');
            indicator.classList.remove('completed');
        } else {
            indicator.classList.remove('active', 'completed');
        }
    });
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

// Function to show an admin modal
function showAdminModal(modalId) {
    // Get all admin modals
    var modals = document.querySelectorAll('.admin-modal');
    var backdrops = document.querySelectorAll('.admin-modal-backdrop');
    
    // Hide any other open modals first
    modals.forEach(function(modal) {
        modal.style.display = 'none';
    });
    
    backdrops.forEach(function(backdrop) {
        backdrop.style.display = 'none';
    });
    
    // Show the targeted modal
    var targetModal = document.querySelector(`#${modalId}`);
    if (!targetModal) {
        // Try finding by class if id doesn't work
        targetModal = document.querySelector(`.admin-modal.show`);
    }
    
    if (targetModal) {
        // Make sure the modal is visible
        targetModal.style.display = 'flex';
        
        // Find the corresponding backdrop
        var backdrop = document.querySelector('.admin-modal-backdrop');
        if (backdrop) {
            backdrop.style.display = 'block';
        }
        
        // Lock body scrolling
        document.body.style.overflow = 'hidden';
    }
}

// Function to hide all admin modals
function hideAdminModals() {
    // Hide all admin modals
    var modals = document.querySelectorAll('.admin-modal');
    modals.forEach(function(modal) {
        modal.style.display = 'none';
    });
    
    // Hide all admin backdrops
    var backdrops = document.querySelectorAll('.admin-modal-backdrop');
    backdrops.forEach(function(backdrop) {
        backdrop.style.display = 'none';
    });
    
    // Restore body scrolling
    document.body.style.overflow = '';
}

// Expose functions to blazor
window.showAdminModal = showAdminModal;
window.hideAdminModals = hideAdminModals; 