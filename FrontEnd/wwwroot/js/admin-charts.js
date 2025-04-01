// Admin dashboard charts - using Chart.js

// Chart instances - global so they persist between calls
window.revenueChart = null;
window.movieChart = null;

window.initializeAdminCharts = function(revenueDataJson, movieDataJson) {
    // Make sure Chart.js is available
    if (typeof Chart === 'undefined') {
        return false;
    }
    
    try {
        // Parse the JSON data
        const revenueData = JSON.parse(revenueDataJson);
        const movieData = JSON.parse(movieDataJson);
        
        // Get the chart canvases
        const revenueCtx = document.getElementById('revenueChartCanvas');
        const movieCtx = document.getElementById('movieChartCanvas');
        
        // Check if canvas elements exist
        if (!revenueCtx) {
            return false;
        }
        
        if (!movieCtx) {
            return false;
        }
        
        // Destroy existing charts if they exist
        if (window.revenueChart) {
            window.revenueChart.destroy();
        }
        
        if (window.movieChart) {
            window.movieChart.destroy();
        }
        
        // Create the revenue chart
        window.revenueChart = new Chart(revenueCtx, {
            type: 'line',
            data: revenueData,
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: false
                    },
                    tooltip: {
                        backgroundColor: 'rgba(0, 0, 0, 0.7)',
                        callbacks: {
                            label: function(context) {
                                return `€${context.raw}`;
                            }
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        grid: {
                            color: 'rgba(0, 0, 0, 0.05)'
                        }
                    },
                    x: {
                        grid: {
                            display: false
                        }
                    }
                }
            }
        });
        
        // Create the movie chart
        window.movieChart = new Chart(movieCtx, {
            type: 'bar',
            data: movieData,
            options: {
                indexAxis: 'y',
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: false
                    }
                },
                scales: {
                    x: {
                        beginAtZero: true,
                        grid: {
                            color: 'rgba(0, 0, 0, 0.05)'
                        }
                    },
                    y: {
                        grid: {
                            display: false
                        }
                    }
                }
            }
        });
        
        return true;
    }
    catch (error) {
        return false;
    }
};

// Update the revenue chart with new data from the API
window.updateRevenueChart = function(newDataJson) {
    if (!window.revenueChart) {
        return;
    }
    
    try {
        const newData = JSON.parse(newDataJson);
        
        // Update the chart data
        window.revenueChart.data.labels = newData.labels;
        window.revenueChart.data.datasets = newData.datasets;
        
        window.revenueChart.update();
    } 
    catch (error) {
        // Silent error handling
    }
};

// Update the movie chart with new data from the API
window.updateMovieChart = function(newDataJson) {
    if (!window.movieChart) {
        return;
    }
    
    try {
        const newData = JSON.parse(newDataJson);
        
        // Update the chart data
        window.movieChart.data.labels = newData.labels;
        window.movieChart.data.datasets = newData.datasets;
        
        window.movieChart.update();
    } 
    catch (error) {
        // Silent error handling
    }
};

// Update revenue chart when time range changes - fallback with hardcoded data
window.updateRevenueChartRange = function(timeRange) {
    if (!window.revenueChart) {
        return;
    }
    
    const days = parseInt(timeRange, 10);
    let labels = [];
    let data = [];
    
    switch (days) {
        case 7:
            labels = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
            data = [2150, 1890, 2340, 3120, 3450, 2980, 2720];
            break;
        case 30:
            labels = Array.from({length: 30}, (_, i) => (i + 1).toString());
            data = Array.from({length: 30}, () => Math.floor(Math.random() * 3000) + 1000);
            break;
        case 90:
            // Group by weeks for 90 days (about 13 weeks)
            labels = Array.from({length: 13}, (_, i) => `Week ${i + 1}`);
            data = Array.from({length: 13}, () => Math.floor(Math.random() * 15000) + 8000);
            break;
        case 365:
            // Group by months for a year
            labels = [
                'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
                'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'
            ];
            data = [
                12500, 11800, 14200, 16500, 18900, 22400,
                25600, 24800, 21300, 18700, 16500, 19200
            ];
            break;
    }
    
    // Update chart data
    window.revenueChart.data.labels = labels;
    window.revenueChart.data.datasets[0].data = data;
    window.revenueChart.update();
};

// Update movie chart when metric changes - fallback with hardcoded data
window.updateMovieChartMetric = function(metric) {
    if (!window.movieChart) {
        return;
    }
    
    const movies = ['Inception', 'Dark Knight', 'Interstellar', 'Pulp Fiction', 'The Godfather'];
    let data = [];
    let label = '';
    
    if (metric === 'tickets') {
        label = 'Tickets Sold';
        data = [350, 290, 260, 240, 180];
    } else if (metric === 'revenue') {
        label = 'Revenue (€)';
        data = [15750, 12650, 11700, 10850, 8100];
    }
    
    // Update chart data
    window.movieChart.data.datasets[0].label = label;
    window.movieChart.data.datasets[0].data = data;
    window.movieChart.update();
}; 