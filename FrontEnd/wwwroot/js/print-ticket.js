async function openPrintDialog(pdfUrl) {
    console.log("Fetching PDF from:", pdfUrl);
    
    try {
        // Show a loading message
        const loadingId = "pdf-loading-message";
        let loadingEl = document.getElementById(loadingId);
        
        if (!loadingEl) {
            loadingEl = document.createElement("div");
            loadingEl.id = loadingId;
            loadingEl.style.position = "fixed";
            loadingEl.style.top = "20px";
            loadingEl.style.right = "20px";
            loadingEl.style.padding = "10px 20px";
            loadingEl.style.background = "rgba(0,0,0,0.7)";
            loadingEl.style.color = "white";
            loadingEl.style.borderRadius = "5px";
            loadingEl.style.zIndex = "9999";
            document.body.appendChild(loadingEl);
        }
        
        loadingEl.textContent = "Loading your tickets...";
        
        // Fetch the PDF with explicit headers
        const response = await fetch(pdfUrl, {
            method: 'GET',
            headers: {
                'Accept': 'application/pdf',
                'Cache-Control': 'no-cache',
                'Pragma': 'no-cache'
            },
            cache: 'no-cache'
        });
        
        // Log response details for debugging
        console.log("Response status:", response.status);
        console.log("Response headers:", [...response.headers].map(h => `${h[0]}: ${h[1]}`).join(", "));
        
        if (!response.ok) {
            throw new Error(`HTTP error! Status: ${response.status}`);
        }
        
        // Check content type
        const contentType = response.headers.get('content-type');
        console.log("Content-Type:", contentType);
        
        if (!contentType || !contentType.includes('application/pdf')) {
            console.warn("Warning: Response may not be a PDF. Content-Type:", contentType);
        }
        
        // Get content length if available
        const contentLength = response.headers.get('content-length');
        console.log("Content-Length:", contentLength || "unknown");
        
        // Get the PDF as an ArrayBuffer
        const pdfBytes = await response.arrayBuffer();
        console.log("Received PDF data, size:", pdfBytes.byteLength, "bytes");
        
        if (pdfBytes.byteLength === 0) {
            throw new Error("Received empty PDF data");
        }
        
        // Create a blob with the correct content type
        const blob = new Blob([pdfBytes], { type: "application/pdf" });
        console.log("Created Blob, size:", blob.size, "bytes");
        
        // Create a URL for the blob
        const blobUrl = URL.createObjectURL(blob);
        console.log("Created Blob URL:", blobUrl);
        
        loadingEl.textContent = "Opening your tickets...";
        
        // Open the PDF in a new tab/window
        const newWindow = window.open(blobUrl, "_blank");
        
        if (!newWindow || newWindow.closed || typeof newWindow.closed === 'undefined') {
            alert("Pop-up blocked! Please allow pop-ups for this site to view your tickets.");
            loadingEl.textContent = "Pop-up blocked! Please allow pop-ups.";
            setTimeout(() => {
                if (document.getElementById(loadingId)) {
                    document.body.removeChild(loadingEl);
                }
            }, 3000);
            return;
        }
        
        // Clean up the URL object after a delay
        setTimeout(() => {
            URL.revokeObjectURL(blobUrl);
            if (document.getElementById(loadingId)) {
                document.body.removeChild(loadingEl);
            }
        }, 5000);
    } catch (error) {
        console.error("Error loading PDF:", error);
        alert("Failed to load the ticket PDF. Please try again or contact support.\n\nError: " + error.message);
    }
}