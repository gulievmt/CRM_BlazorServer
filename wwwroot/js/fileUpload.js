/**
 * fileUpload.js
 * Uploads files directly from the browser to the ASP.NET controller via XMLHttpRequest,
 * completely bypassing the Blazor SignalR connection (and its 10 MB message-size limit).
 */

/**
 * Attach an onchange listener to the hidden <input type="file"> so that when the
 * user picks a file, Blazor is notified with the file name and size.
 *
 * @param {string}   inputId   - id of the <input type="file"> element
 * @param {any}      dotNetRef - DotNetObjectReference to call [JSInvokable] methods on
 */
window.fileUpload_init = function (inputId, dotNetRef) {
    const input = document.getElementById(inputId);
    if (!input) return;
    input.onchange = function () {
        if (input.files && input.files.length > 0) {
            const f = input.files[0];
            dotNetRef.invokeMethodAsync('OnFileSelected', f.name, f.size);
        }
    };
};

/**
 * Trigger the browser's native file-picker dialog by clicking the hidden input.
 *
 * @param {string} inputId - id of the <input type="file"> element
 */
window.fileUpload_choose = function (inputId) {
    const input = document.getElementById(inputId);
    if (input) input.click();
};

/**
 * Upload the selected file to the given URL via XHR multipart/form-data POST.
 * Progress events are forwarded to Blazor via dotNetRef.
 *
 * @param {string} inputId   - id of the <input type="file"> element
 * @param {string} uploadUrl - server endpoint, e.g. "/upload/large"
 * @param {any}    dotNetRef - DotNetObjectReference
 * @returns {Promise<string>} - JSON response text from the server on success
 */
window.fileUpload_upload = function (inputId, uploadUrl, dotNetRef) {
    return new Promise(function (resolve, reject) {
        const input = document.getElementById(inputId);
        if (!input || !input.files || !input.files.length) {
            reject('Файл не выбран.');
            return;
        }

        const file = input.files[0];
        const formData = new FormData();
        formData.append('file', file);

        const xhr = new XMLHttpRequest();

        // Report upload progress back to Blazor
        xhr.upload.onprogress = function (e) {
            if (e.lengthComputable) {
                const pct = Math.round((e.loaded / e.total) * 100);
                dotNetRef.invokeMethodAsync('OnUploadProgress', pct);
            }
        };

        xhr.onload = function () {
            // Clear the input so the same file can be re-selected if needed
            input.value = '';
            if (xhr.status >= 200 && xhr.status < 300) {
                resolve(xhr.responseText);
            } else {
                reject(xhr.responseText || xhr.statusText || 'Ошибка сервера: ' + xhr.status);
            }
        };

        xhr.onerror = function () {
            reject('Ошибка сети при загрузке файла.');
        };

        xhr.open('POST', uploadUrl);
        // Don't set Content-Type — browser sets it automatically with the multipart boundary
        xhr.send(formData);
    });
};
