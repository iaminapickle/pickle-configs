local group = vim.api.nvim_create_augroup("pdf_viewer", { clear = true })

vim.api.nvim_create_autocmd("BufReadCmd", {
  group = group,
  pattern = "*.pdf",
  callback = function(event)
    local buf = event.buf
    local path = vim.fn.expand(event.match)

    local text = vim.fn.system({ "pdftotext", "-layout", path, "-" })
    local lines = vim.split(text, "\n", { plain = true })

    vim.bo[buf].modifiable = true
    vim.api.nvim_buf_set_lines(buf, 0, -1, true, lines)
    vim.bo[buf].modifiable = false
    vim.bo[buf].buftype = "nowrite"
    vim.bo[buf].swapfile = false
    vim.bo[buf].filetype = "pdf_preview"
    vim.opt_local.wrap = false
  end,
})
