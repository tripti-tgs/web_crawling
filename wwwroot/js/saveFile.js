

async function saveFile() {
    try {
        alert("hello")
        // Show directory picker
        const dirHandle = await window.showDirectoryPicker();

        // Get file name from input field or use default
        const fileNameInput = document.getElementById('fileName');
        const fileName = fileNameInput.value.trim() || "scraped_data";

        // Ensure fileName is valid
        const safeFileName = fileName;

        // Create or open a file in the selected directory
        const fileHandle = await dirHandle.getFileHandle(`${safeFileName}.json`, { create: true });

        // Create a writable stream for the file
        const writable = await fileHandle.createWritable();

        // Your JSON data (from ViewBag or another source)
        const jsonData = @Html.Raw(JsonConvert.SerializeObject(ViewBag.TextValue));

        // Write the JSON data to the file
        await writable.write(new Blob([jsonData], { type: 'application/json' }));

        // Close the writable stream
        await writable.close();

        alert('File saved successfully!');
    } catch (err) {
        console.error('Error saving file:', err);
    }
}
