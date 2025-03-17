async function openPrintDialog(pdfUrl) {

    try {
        const response = await fetch(pdfUrl);
        const pdfBytes = await response.arrayBuffer();
        const blob = new Blob([pdfBytes], { type: "application/pdf "});
        const blobUrl = URL.createObjectURL(blob);

        window.open(blobUrl, "_blank");

    } catch (error) {
        console.error("Error loading PDF:", error)
    }
    
}