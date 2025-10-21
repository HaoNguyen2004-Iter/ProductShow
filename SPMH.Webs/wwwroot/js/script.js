const loadingHtml = '<div class="d-flex justify-content-center py-5"><div class="spinner-border" role="status"><span class="visually-hidden">Loading...</span></div></div>';
const tableSel = '#productTableWrap';

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

(function ($) {
    function buildSearchQuery() {
        const $form = $('#searchForm');
        if ($form.length === 0) return '';

        const text = ($('#searchText').val() || '').trim();
        const brand = ($('#brandFilter').val() || '').trim();
        const price = ($('#Price').val() || '').trim();
        const stock = ($('#Stock').val() || '').trim();
        const stRaw = ($('#statusFilter').val() || '').trim();

        const params = new URLSearchParams();

        if (text) {
            params.append('name', text);
            params.append('code', text);
        }
        if (brand) params.append('brand', brand);
        if (price) params.append('price', price);
        if (stock) params.append('stock', stock);

        if (stRaw === 'active') params.append('status', '1');
        else if (stRaw === 'inactive') params.append('status', '0');

        return params.toString();
    }
    function buildSearchUrl(base = '/Product/Index') {
        const qs = buildSearchQuery();
        return base + (qs ? ('?' + qs) : '');
    }
    function debounce(fn, ms) {
        let t;
        return function () {
            clearTimeout(t);
            const args = arguments, ctx = this;
            t = setTimeout(() => fn.apply(ctx, args), ms);
        };
    }

    let isComposing = false;
    $(document).on('compositionstart', '#searchText', function () {
        isComposing = true;
    });
    $(document).on('compositionend', '#searchText', function () {
        isComposing = false;
        $('#searchForm').trigger('submit');
    });

    // Submit form tìm kiếm => load lại bảng
    $(document).on('submit', '#searchForm', function (e) {
        e.preventDefault();
        loadTable(buildSearchUrl());
    });

    // Tự động tìm khi đổi select/number
    $(document).on('change', '#brandFilter, #statusFilter, #Price, #Stock', function () {
        $('#searchForm').trigger('submit');
    });

    // Debounce khi gõ ô từ khóa
    $(document).on('input', '#searchText', debounce(function () {
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

    // ========== Các handler sẵn có ==========
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

    // Xử lý nút lưu cho Create và Edit
    $(document).on('click', '#btnSaveProduct', function () {
        const $wrap = $('#productForm');
        if ($wrap.length === 0) return;

        const mode = $wrap.data('mode');
        const id = Number($wrap.data('id') || 0);

        const read = (name) => ($wrap.find('[name="' + name + '"]').val() || '').toString().trim();
        const readNumber = (name) => {
            const v = $wrap.find('[name="' + name + '"]').val();
            return v === '' || v == null ? 0 : Number(v);
        };
        const readChecked = (name) => $wrap.find('[name="' + name + '"]').is(':checked');

        const payload = {
            Id: id,
            Code: read('Code'),
            Name: read('Name'),
            BrandName: read('BrandName'),
            PriceVnd: readNumber('PriceVnd'),
            Stock: readNumber('Stock'),
            Description: read('Description'),
            Url: '',
            Status: readChecked('Status') ? 1 : 0
        };

        const url = mode === 'edit' ? '/Product/Edit' : '/Product/Create';
        const $btn = $(this).prop('disabled', true);

        const fileInput = $wrap.find('input[name="ProductImage"]');
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
                    // Giữ bộ lọc hiện tại sau khi lưu
                    loadTable(buildSearchUrl());
                })
                .fail(function (xhr) {
                    const res = xhr.responseJSON;
                    const msg = (res && res.error) || xhr.responseText || 'Lỗi khi lưu sản phẩm';
                    $('#modalBody').find('.alert').remove();
                    $('#modalBody').prepend('<div class="alert alert-danger mb-3">' + msg + '</div>');
                    $('#modalBody').scrollTop(0);
                })
                .always(function () {
                    $btn.prop('disabled', false);
                });
        }
        if (file) {
            const fd = new FormData();
            fd.append('file', file);
            $.ajax({
                url: '/Product/Upload',
                method: 'POST',
                data: fd,
                processData: false,
                contentType: false,
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            })
                .done(function (res) {
                    if (res && res.ok && res.url) {
                        payload.Url = res.url;
                    }
                    saveProduct();
                })
                .fail(function (xhr) {
                    const res = xhr.responseJSON;
                    const msg = (res && res.error) || xhr.responseText || 'Tải ảnh thất bại';
                    $('#modalBody').find('.alert').remove();
                    $('#modalBody').prepend('<div class="alert alert-danger mb-3">' + msg + '</div>');
                    $('#modalBody').scrollTop(0);
                    $btn.prop('disabled', false);
                });
        } else {
            saveProduct()
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

    // Lấy danh sách id đã chọn
    function getSelectedIds() {
        const ids = [];
        document.querySelectorAll('.js-chk-item:checked').forEach(el => {
            const v = parseInt(el.value, 10);
            if (!isNaN(v)) ids.push(v);
        });
        return ids;
    }

    // Cập nhật trạng thái nút Bulk + checkbox chọn tất cả
    function updateBulkState() {
        const total = document.querySelectorAll('.js-chk-item').length;
        const checked = document.querySelectorAll('.js-chk-item:checked').length;

        $('#btnBulkDelete').prop('disabled', checked === 0);

        const $all = $('#chkSelectAll');
        $all.prop('indeterminate', checked > 0 && checked < total);
        $all.prop('checked', total > 0 && checked === total);
    }

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

    // Phân trang: giữ lại bộ lọc hiện tại
    $(document).on('click', '.pagination .page-link', function (e) {
        const href = this.getAttribute('href');
        if (!href || this.closest('.page-item')?.classList.contains('disabled')) return;
        e.preventDefault();

        const qs = buildSearchQuery();
        const url = href + (qs ? (href.includes('?') ? '&' : '?') + qs : '');
        loadTable(url);
    });

})(jQuery);