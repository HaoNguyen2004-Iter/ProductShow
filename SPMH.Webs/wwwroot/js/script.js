const loadingHtml = '<div class="d-flex justify-content-center py-5"><div class="spinner-border" role="status"><span class="visually-hidden">Loading...</span></div></div>';
const tableSel = '#productTableWrap';
function hasBadString(value) {
    return badString.test(value)
}
function debounce(fn, ms) {
    let t;
    return function () {
        clearTimeout(t);
        const args = arguments, ctx = this;
        t = setTimeout(() => fn.apply(ctx, args), ms);
    };
}
function buildSearchQuery() {
    const $form = $('#searchForm');
    if ($form.length === 0) return '';

    const text = ($('#searchText').val() || '').trim();
    const brand = ($('#brandFilter').val() || '').trim();
    const priceFrom = ($('#PriceFrom').val() || '').trim();
    const priceTo = ($('#PriceTo').val() || '').trim();
    const stockFrom = ($('#StockFrom').val() || '').trim();
    const stockTo = ($('#StockTo').val() || '').trim();
    const stRaw = ($('#statusFilter').val() || '').trim();

    const params = new URLSearchParams();

    if (text) params.append('Keyword', text);
    if (brand) params.append('BrandName', brand);
    if (priceFrom) params.append('PriceFrom', priceFrom);
    if (priceTo) params.append('PriceTo', priceTo);
    if (stockFrom) params.append('StockFrom', stockFrom);
    if (stockTo) params.append('StockTo', stockTo);

    if (stRaw === '1') params.append('Status', '1');
    else if (stRaw === '0') params.append('Status', '0');

    return params.toString();
}
function buildSearchUrl(base = '/Product/Index') {
    const qs = buildSearchQuery();
    return base + (qs ? ('?' + qs) : '');
}

function loadTable(url) {
    if (!url) url = '/Product/Index';
    const $wrap = $(tableSel);
    $wrap.html(loadingHtml);

    $.ajax({
        url: url,
        method: 'GET',
        headers: { 'X-Requested-With': 'XMLHttpRequest' }
    })
        .done(function (html) {
            $wrap.html(html);
        })
        .fail(function (xhr) {
            const msg = xhr.responseText || 'Có lỗi xảy ra khi tải bảng.';
            $wrap.html('<div class="alert alert-danger">' + msg + '</div>');
        });
}

function showTempAlert(message, type = 'success', ms = 2000) {
    const $alert = $('<div class="alert alert-' + type + ' position-fixed top-0 start-50 translate-middle-x mt-3 shadow" style="z-index:1080"></div>').text(message);
    $('body').append($alert);
    setTimeout(() => $alert.fadeOut(200, () => $alert.remove()), ms);
}

function getReloadUrl() {
    const el = document.getElementById('btnReload');
    return (el && (el.getAttribute('data-href') || el.getAttribute('href'))) || location.href;
}

function getSelectedIds() {
    const ids = [];
    document.querySelectorAll('.js-chk-item:checked').forEach(el => {
        const v = parseInt(el.value, 10);
        if (!isNaN(v)) ids.push(v);
    });
    return ids;
}

function updateBulkState() {
    const total = document.querySelectorAll('.js-chk-item').length;
    const checked = document.querySelectorAll('.js-chk-item:checked').length;

    $('#btnBulkDelete').prop('disabled', checked === 0);

    const $all = $('#chkSelectAll');
    $all.prop('indeterminate', checked > 0 && checked < total);
    $all.prop('checked', total > 0 && checked === total);
}

async function uploadFileInChunks(file) {
    const chunkSize = 500 * 1024; // 500 KB
    const MaxTotalFileSize = 5 * 1024 * 1024; // 5 MB

    if (!file || typeof file.size !== 'number') throw new Error('file không khả dụng');
    if (file.size > MaxTotalFileSize) throw new Error('Không được up ảnh quá 5mb');

    const totalChunks = Math.max(1, Math.ceil(file.size / chunkSize));
    const fileCode = (crypto && crypto.randomUUID)
        ? crypto.randomUUID().replaceAll('-', '')
        : String(Date.now()) + '_' + Math.floor(Math.random() * 1000000);

    let uploadedBytes = 0;

    for (let i = 0; i < totalChunks; i++) {
        const start = i * chunkSize;
        const end = Math.min(file.size, start + chunkSize);
        const blob = file.slice(start, end);

        const fd = new FormData();
        fd.append('chunk', blob, file.name);
        fd.append('fileCode', fileCode);
        fd.append('chunkIndex', i.toString());

        let resp;
        try {
            resp = await fetch('/Product/UploadChunk', {
                method: 'POST',
                body: fd,
                credentials: 'same-origin'
            });
        } catch (fetchErr) {
            throw new Error('lỗi mạng' + i + (fetchErr?.message ? (': ' + fetchErr.message) : ''));
        }

        if (!resp.ok) {
            let errMsg = `Chunk ${i} upload lỗi (${resp.status})`;
            try {
                const ct = resp.headers.get('Content-Type') || '';
                if (ct.includes('application/json')) {
                    const j = await resp.json();
                    errMsg = j && (j.error || j.message) ? (j.error || j.message) : errMsg;
                } else {
                    const txt = await resp.text();
                    if (txt) errMsg = txt;
                }
            } catch {
                throw new Error(errMsg);
            }
        }
        uploadedBytes += (end - start);
        // no onProgress callback anymore
    }

    const fdComplete = new FormData();
    fdComplete.append('fileCode', fileCode);
    fdComplete.append('fileName', file.name);
    fdComplete.append('totalChunks', totalChunks.toString());

    let respComplete;
    try {
        respComplete = await fetch('/Product/CompleteUpload', {
            method: 'POST',
            body: fdComplete,
            credentials: 'same-origin'
        });
    } catch (fetchErr) {
        throw new Error('Lỗi mạng' + (fetchErr?.message ? (' ' + fetchErr.message) : ''));
    }

    if (!respComplete.ok) {
        let errMsg = `(${respComplete.status})`;
        try {
            const ct = respComplete.headers.get('Content-Type') || '';
            if (ct.includes('application/json')) {
                const j = await respComplete.json();
                errMsg = j && (j.error || j.message) ? (j.error || j.message) : errMsg;
            } else {
                const txt = await respComplete.text();
                if (txt) errMsg = txt;
            }
        } catch {
            throw new Error(errMsg);
        }
    }

    try {
        return await respComplete.json();
    } catch {
        return { ok: true, url: await respComplete.text() };
    }
}

(function ($) {
    // Đăng nhập
    $(document).on('click', '#btnLogin', function (e) {
        e.preventDefault();
        const $wrap = $('#LoginForm');
        const f = Spmh.fields.reader($wrap);

        const $btn = $(this);
        const username = f.read('Username').toString().trim();
        const password = f.read('Password').toString().trim();

        if (!username || !password) {
            showTempAlert('Vui lòng nhập Tên đăng nhập và Mật khẩu.', 'warning');
            return;
        }

        const payload = { Username: username, Password: password };

        const originalHtml = $btn.html();
        $btn.prop('disabled', true).html('Đang đăng nhập...');

        $.ajax({
            url: '/Account/Login',
            method: 'POST',
            data: payload,
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .done(function (xhr) {
                const redirectUrl = (xhr && xhr.responseURL) || '/Product/Index';
                window.location.assign(redirectUrl);
            })
            .fail(function (xhr) {
                const res = xhr.responseJSON;
                const msg = (res && (res.error || res.message)) || xhr.responseText || 'Đăng nhập thất bại';
                showTempAlert(msg, 'danger', 3000);
            })
            .always(function () {
                $btn.prop('disabled', false).html(originalHtml);
            });
    });

    // Đăng xuất
    $(document).on('click', '#btnLogout', function (e) {
        e.preventDefault();
        const $btn = jQuery(this);
        const originalHtml = $btn.html();

        $btn.prop('disabled', true).html('Đang đăng xuất...');

        $.ajax({
            url: '/Account/Logout',
            method: 'POST',
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .done(function () {
                window.location.assign('/Account/Login');
            })
            .fail(function (xhr) {
                const res = xhr.responseJSON;
                const msg = (res && (res.error || res.message)) || xhr.responseText || 'Đăng xuất thất bại';
                if (typeof showTempAlert === 'function') showTempAlert(msg, 'danger', 3000);
            })
            .always(function () {
                $btn.prop('disabled', false).html(originalHtml);
            });
    })

    let isComposing = false;

    // Kiểm tra nhập 
    $(document).on('compositionstart', '#searchText', function () {
        isComposing = true;
    });
    $(document).on('compositionend', '#searchText', function () {
        isComposing = false;
        $('#searchForm').trigger('submit');
    });

    // Submit form tìm kiếm => load lại bảng
    $(document).on('submit', '#searchForm', function (e) {
        const text = ($('#searchText').val() || '').toString();
        e.preventDefault();
        loadTable(buildSearchUrl());
    });

    // Tự động tìm khi đổi select/number
    $(document).on('change', '#brandFilter, #statusFilter, #PriceFrom, #PriceTo, #StockFrom, #StockTo', function () {
        $('#searchForm').trigger('submit');
    });

    // Debounce khi gõ ô từ khóa
    $(document).on('input', '#searchText', debounce(function () {
        if (isComposing) return;
        $('#searchForm').trigger('submit');
    }, 400));

    // Reset lọc
    $(document).on('click', '#resetBtn', function () {
        const $f = $('#searchForm');
        $f[0]?.reset();
        $f.find('input[type="text"], input[type="number"]').val('');
        $f.find('select').val('');
        $f.trigger('submit');
    });

    // Sự kiện click mở Create hoặc Edit
    $(document).on('click', '[data-bs-target="#modalCreate"], [data-bs-target="#modalEdit"]', function (e) {
        e.preventDefault();
        const isEdit = this.getAttribute('data-bs-target') === '#modalEdit';
        const id = this.getAttribute('data-id');
        const url = isEdit ? ('/Product/Form?id=' + encodeURIComponent(id)) : '/Product/Form';

        const $modal = $('#modalOpen');
        const modal = bootstrap.Modal.getOrCreateInstance($modal[0]);
        $modal.find('.modal-title').text(isEdit ? 'Sửa sản phẩm' : 'Thêm sản phẩm');
        $('#modalBody').html(loadingHtml);

        $.ajax({
            url: url,
            method: 'GET',
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .done(function (html) {
                $('#modalBody').html(html);
                modal.show();
            })
            .fail(function (xhr) {
                $('#modalBody').html('<div class="alert alert-danger">' + (xhr.responseText || 'Không tải được form') + '</div>');
                modal.show();
            });
    });

    // Cập nhật nhãn trạng thái khi bật/tắt trong Edit và Create
    $(document).on('change', '#chkStatus', function () {
        const $lbl = $(this).siblings('label[for="chkStatus"]');
        $lbl.text(this.checked ? 'Hoạt động' : 'Dừng bán');
    });

    // Preview ảnh ngay khi chọn file
    $(document).on('change', 'input[name="ProductImage"]', function () {
        const file = this.files && this.files[0];
        const $col = $(this).closest('.col-12');

        // Tìm ảnh preview có sẵn (nếu đã render từ server), nếu không có thì tạo mới
        let $previewImg = $col.find('img').first();
        if ($previewImg.length === 0) {
            // Tạo khung preview sau input-group
            let $box = $col.find('#jsPreviewBox');
            if ($box.length === 0) {
                $box = $('<div id="jsPreviewBox" class="mt-2"></div>').insertAfter($(this).closest('.input-group'));
            }
            $previewImg = $('<img class="img-fluid rounded shadow-sm" style="max-height: 150px; object-fit: contain;" />');
            $box.empty().append($previewImg);
        }

        // Lưu src gốc để có thể khôi phục khi xoá chọn file
        if (!$previewImg.data('orig')) {
            $previewImg.data('orig', $previewImg.attr('src') || '');
        }

        if (!file) {
            // Không có file (đã xoá chọn): khôi phục ảnh gốc hoặc xoá preview nếu không có gốc
            const orig = $previewImg.data('orig');
            if (orig) $previewImg.attr('src', orig);
            else $previewImg.closest('#jsPreviewBox').remove();
            return;
        }

        if (!file.type || !file.type.startsWith('image/')) {
            showTempAlert('Vui lòng chọn tệp ảnh hợp lệ.', 'warning');
            this.value = '';
            const orig = $previewImg.data('orig');
            if (orig) $previewImg.attr('src', orig);
            else $previewImg.closest('#jsPreviewBox').remove();
            return;
        }

        const blobUrl = URL.createObjectURL(file);
        $previewImg.attr('src', blobUrl).one('load', function () {
            URL.revokeObjectURL(blobUrl);
        });
    });

    // Khi bấm nút "Xóa" cạnh ô file thì xoá preview luôn
    $(document).on('click', '.input-group .btn.btn-outline-secondary', function () {
        const $input = $(this).siblings('input[type="file"][name="ProductImage"]');
        if ($input.length) {
            $input.val('');
            $input.trigger('change');
        }
    });

    debugger;
    // Xử lý nút lưu cho Create và Edit
    $(document).on('click', '#btnSaveProduct', async function () {
        const $wrap = $('#productForm');
        if ($wrap.length === 0) return;

        const f = Spmh.fields.reader($wrap);
        const mode = $wrap.data('mode');
        const id = Number($wrap.data('id') || 0);

        const payload = {
            Id: id,
            Code: f.read('Code'),
            Name: f.read('Name'),
            BrandName: f.read('BrandName'),
            PriceVnd: f.readNumber('PriceVnd'),
            Stock: f.readNumber('Stock'),
            Description: f.read('Description'),
            Url: '',
            Status: f.readChecked('Status') ? 1 : 0
        };

        const url = mode === 'edit' ? '/Product/Edit' : '/Product/Create';
        const $btn = $(this);
        const originalBtnHtml = $btn.html();
        $btn.prop('disabled', true);

        const fileInput = $wrap.find('input[name="ProductImage"]')[0];
        const file = fileInput && fileInput.files && fileInput.files.length ? fileInput.files[0] : null;

        function saveProduct() {
            $.ajax({
                url: url,
                method: 'POST',
                data: JSON.stringify(payload),
                contentType: 'application/json; charset=utf-8',
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            })
                .done(function (res) {
                    const $modal = $('#modalOpen');
                    const modal = bootstrap.Modal.getInstance($modal[0]);
                    if (modal) modal.hide();

                    const msg = (res && (res.message || res.error)) || 'Lưu thành công';
                    showTempAlert(msg, 'success');
                    loadTable(buildSearchUrl());
                })
                .fail(function (xhr) {
                    const res = xhr.responseJSON;
                    const msg = (res && res.error) || xhr.responseText;
                    $('#modalBody').find('.alert').remove();
                    $('#modalBody').prepend('<div class="alert alert-danger mb-3">' + msg + '</div>');
                    $('#modalBody').scrollTop(0);
                })
                .always(function () {
                    $btn.prop('disabled', false).html(originalBtnHtml);
                });
        }

        if (file) {
            try {
                const res = await uploadFileInChunks(file);
                if (res && res.url) payload.Url = res.url;
                $btn.html(originalBtnHtml);
                saveProduct();
            } catch (err) {
                $('#modalBody').find('.alert').remove();
                $('#modalBody').prepend('<div class="alert alert-danger mb-3">' + (err && err.message ? err.message : String(err)) + '</div>');
                $('#modalBody').scrollTop(0);
                $btn.prop('disabled', false).html(originalBtnHtml);
            }
        } else {
            saveProduct();
        }
    });

    // Sự kiện click mở Detail
    $(document).on('click', '[data-bs-target="#modalDetail"]', function (e) {
        e.preventDefault();
        const id = this.getAttribute('data-id');
        if (!id) return;

        const $modal = $('#modalOpen');
        const modal = bootstrap.Modal.getOrCreateInstance($modal[0]);
        $modal.find('.modal-title').text('Chi tiết sản phẩm');
        $('#modalBody').html(loadingHtml);

        $.ajax({
            url: '/Product/Detail?id=' + encodeURIComponent(id),
            method: 'GET',
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .done(function (html) {
                $('#modalBody').html(html);
                const name = $('#modalBody').find('#__pd_name').text()?.trim();
                if (name) $modal.find('.modal-title').text(name);
                modal.show();
            })
            .fail(function (xhr) {
                $('#modalBody').html('<div class="alert alert-danger">' + (xhr.responseText || 'Không tải được chi tiết') + '</div>');
                modal.show();
            });
    });

    // Sự kiện click mở Delete
    $(document).on('click', '[data-bs-target="#modalDelete"]', function (e) {
        e.preventDefault();
        const id = this.getAttribute('data-id');
        if (!id) return;

        const $modal = $('#modalOpen');
        const modal = bootstrap.Modal.getOrCreateInstance($modal[0]);
        $modal.find('.modal-title').text('Xóa sản phẩm');

        const confirmHtml = `
            <div id="deleteBox" data-id="${id}">
                <p class="mb-3">Bạn có chắc chắn muốn xóa sản phẩm</p>
                <div class="text-end">
                    <button type="button" class="btn btn-outline-secondary btn-sm" data-bs-dismiss="modal">Hủy</button>
                    <button type="button" class="btn btn-danger btn-sm" id="btnConfirmDelete">Xóa</button>
                </div>
            </div>`;
        $('#modalBody').html(confirmHtml);
        modal.show();
    });

    // Xử lý nút Xóa
    $(document).on('click', '#btnConfirmDelete', function () {
        const id = Number($('#deleteBox').data('id') || 0);
        if (!id) return;

        const $btn = $(this).prop('disabled', true);
        $.ajax({
            url: '/Product/Delete',
            method: 'POST',
            data: { id: id },
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .done(function (res) {
                bootstrap.Modal.getInstance(document.getElementById('modalOpen'))?.hide();
                showTempAlert((res && res.message) || 'Xóa thành công', 'success');
                // Giữ bộ lọc hiện tại sau khi xóa
                loadTable(buildSearchUrl());
            })
            .fail(function (xhr) {
                const msg = (xhr.responseJSON && xhr.responseJSON.error) || xhr.responseText || 'Xóa thất bại';
                showTempAlert(msg, 'danger');
            })
            .always(function () {
                $btn.prop('disabled', false);
            });
    });

    // Chọn tất cả cho xoá nhiều
    $(document).on('change', '#chkSelectAll', function () {
        const checked = this.checked;
        $('.js-chk-item').prop('checked', checked);
        updateBulkState();
    });

    // Khi đổi trạng thái từng item
    $(document).on('change', '.js-chk-item', function () {
        updateBulkState();
    });

    // Mở modal xác nhận xóa nhiều
    $(document).on('click', '#btnBulkDelete', function (e) {
        e.preventDefault();
        const ids = getSelectedIds();
        if (ids.length === 0) {
            showTempAlert('Vui lòng chọn sản phẩm để xóa', 'warning');
            return;
        }

        const $modal = $('#modalOpen');
        const modal = bootstrap.Modal.getOrCreateInstance($modal[0]);
        $modal.find('.modal-title').text('Xóa nhiều sản phẩm');

        const html = `
            <div id="bulkDeleteBox">
                <p class="mb-2">Bạn có chắc chắn muốn xóa các sản phẩm đã chọn? </p>
                <div class="text-end">
                    <button type="button" class="btn btn-outline-secondary btn-sm" data-bs-dismiss="modal">Hủy</button>
                    <button type="button" class="btn btn-danger btn-sm" id="btnConfirmBulkDelete">Xóa tất cả</button>
                </div>
            </div>`;
        $('#modalBody').html(html);
        $('#bulkDeleteBox').data('ids', ids);
        modal.show();
    });

    // Gửi yêu cầu xóa nhiều
    $(document).on('click', '#btnConfirmBulkDelete', function () {
        const ids = $('#bulkDeleteBox').data('ids') || [];
        if (!ids.length) return;

        const $btn = $(this).prop('disabled', true);
        $.ajax({
            url: '/Product/BulkDelete',
            method: 'POST',
            data: { ids: ids },
            traditional: true,
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .done(function (res) {
                bootstrap.Modal.getInstance(document.getElementById('modalOpen'))?.hide();
                showTempAlert((res && res.message) || 'Đã xóa thành công', 'success');
                // Giữ bộ lọc hiện tại sau khi xóa nhiều
                loadTable(buildSearchUrl());
            })
            .fail(function (xhr) {
                const msg = (xhr.responseJSON && xhr.responseJSON.error) || xhr.responseText || 'Xóa nhiều thất bại';
                showTempAlert(msg, 'danger');
            })
            .always(function () {
                $btn.prop('disabled', false);
            });
    });

    // Phân trang
    $(document).on('click', '.pagination .page-link', function (e) {
        const href = this.getAttribute('href');
        if (!href || this.closest('.page-item')?.classList.contains('disabled')) return;
        e.preventDefault();

        const qs = buildSearchQuery();
        const url = href + (qs ? (href.includes('?') ? '&' : '?') + qs : '');
        loadTable(url);
    });

    // Nhập Excel
    $(document).on('click', '#btnImportExcel', async function (e) {
        e.preventDefault();
        const $btn = $(this).prop('disabled', true);
        try {
            const input = document.getElementById('importFile');
            if (!input || !input.files || input.files.length === 0) {
                showTempAlert('Vui lòng chọn tệp .xlsx hoặc .csv', 'warning');
                return;
            }
            const file = input.files[0];
            const fd = new FormData();
            fd.append('file', file);

            const resp = await fetch('/Product/Import', { method: 'POST', body: fd, credentials: 'same-origin' });
            if (!resp.ok) {
                let txt = '';
                try { txt = await resp.text(); } catch { }
                showTempAlert(txt || 'Nhập lỗi', 'danger', 5000);
                return;
            }

            const data = await resp.json();
            if (data && data.ok) {
                showTempAlert(data.message || 'Nhập thành công', 'success', 4000);
                if (Array.isArray(data.errors) && data.errors.length) {
                    console.group('Nhập lỗi');
                    data.errors.forEach(err => console.warn(err));
                    console.groupEnd();
                }
                loadTable(buildSearchUrl());
            } else {
                const msg = (data && (data.error || data.message)) || 'Nhập xong nhưng gặp vấn đề';
                showTempAlert(msg, 'warning', 5000);
                if (data && Array.isArray(data.errors) && data.errors.length) {
                    console.group('Nhập lỗi');
                    data.errors.forEach(err => console.warn(err));
                    console.groupEnd();
                }
            }
        } catch (err) {
            console.error(err);
            showTempAlert('Lỗi khi nhập file', 'danger', 5000);
        } finally {
            $btn.prop('disabled', false);
        }
    });

    // Xuất Excel toàn bảng
    $(document).on('click', '#btnExportCsv', async function () {
        const baseUrl = '/Product/ExportCsvNpoi';

        try {
            const qs = buildSearchQuery();
            const url = baseUrl + (qs ? ('?' + qs) : '');

            const resp = await fetch(url, { method: 'GET', credentials: 'same-origin' });

            // 3. Xử lý lỗi
            if (!resp.ok) {
                let err = '';
                try { err = await resp.text(); } catch { }
                if (typeof showTempAlert === 'function')
                    showTempAlert(err || 'Export failed', 'danger', 3000);
                return;
            }

            // 4. Lấy tên file từ header
            let fileName = null;
            const cd = resp.headers.get('Content-Disposition') || '';
            let m = cd.match(/filename\*=UTF-8''([^;]+)/i);
            if (m && m[1]) fileName = decodeURIComponent(m[1]);
            else {
                m = cd.match(/filename="?([^";]+)"?/i);
                fileName = m ? m[1] : null;
            }

            // 5. Nếu không có tên → tạo mặc định theo type
            if (!fileName) {
                const ts = new Date().toISOString().replace(/[:.]/g, '-').replace('T', '_').split('Z')[0];
                const contentType = resp.headers.get('Content-Type') || '';
                if (contentType.includes('spreadsheetml.sheet') || contentType.includes('excel')) {
                    fileName = `products_${ts}.xlsx`;
                } else if (contentType.includes('csv')) {
                    fileName = `products_${ts}.csv`;
                } else {
                    fileName = `download_${ts}`;
                }
            }

            // 6. Tải file
            const blob = await resp.blob();
            const downloadUrl = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = downloadUrl;
            link.download = fileName;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            URL.revokeObjectURL(downloadUrl);

        } catch (e) {
            console.error(e);
            if (typeof showTempAlert === 'function')
                showTempAlert('Lỗi khi tải file', 'danger', 3000);
        }
    });

    // Xuất PDF chi tiết sản phẩm
    $(document).on('click', '#btnExportPdf', async function (e) {
        e.preventDefault();
        const id = this.getAttribute('data-id');
        if (!id) return;

        const url = '/Product/ExportPdf?id=' + encodeURIComponent(id);
        try {
            const resp = await fetch(url, { method: 'GET', credentials: 'same-origin' });

            if (!resp.ok) {
                let err = '';
                try { err = await resp.text(); } catch { }
                if (typeof showTempAlert === 'function') showTempAlert(err || 'Xuất PDF thất bại', 'danger', 3000);
                return;
            }

            const blob = await resp.blob();

            let fileName = null;
            const cd = resp.headers.get('Content-Disposition') || '';
            let m = cd.match(/filename\*=UTF-8''([^;]+)/i);
            if (m && m[1]) fileName = decodeURIComponent(m[1]);
            else {
                m = cd.match(/filename="?([^";]+)"?/i);
                fileName = m ? m[1] : null;
            }

            if (!fileName) {
                const ts = new Date().toISOString().replace(/[:.]/g, '-').replace('T', '_').split('Z')[0];
                fileName = `product_${id}_${ts}.pdf`;
            }

            const downloadUrl = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = downloadUrl;
            link.download = fileName;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            URL.revokeObjectURL(downloadUrl);
        } catch (err) {
            console.error(err);
            if (typeof showTempAlert === 'function') showTempAlert('Lỗi khi tải PDF', 'danger', 3000);
        }
    });

    // Xuất Word chi tiết sản phẩm
    $(document).on('click', '#btnExportWord', async function (e) {
        e.preventDefault();
        const id = this.getAttribute('data-id');
        if (!id) return;

        const url = '/Product/ExportWord?id=' + encodeURIComponent(id);
        try {
            const resp = await fetch(url, { method: 'GET', credentials: 'same-origin' });
            if (!resp.ok) {
                let err = '';
                try { err = await resp.text(); } catch { }
                if (typeof showTempAlert === 'function') showTempAlert(err || 'Xuất Word thất bại', 'danger', 3000);
                return;
            }
            const blob = await resp.blob();
            let fileName = `product_${id}_${new Date().toISOString().replace(/[:.]/g, '-')}.docx`;
            const cd = resp.headers.get('Content-Disposition') || '';
            let m = cd.match(/filename\*=UTF-8''([^;]+)/i);
            if (m && m[1]) fileName = decodeURIComponent(m[1]);
            else {
                m = cd.match(/filename="?([^";]+)"?/i);
                if (m && m[1]) fileName = m[1];
            }
            const downloadUrl = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = downloadUrl;
            link.download = fileName;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            URL.revokeObjectURL(downloadUrl);
        } catch (err) {
            console.error(err);
            if (typeof showTempAlert === 'function') showTempAlert('Lỗi khi tải Word', 'danger', 3000);
        }
    });

})(jQuery);