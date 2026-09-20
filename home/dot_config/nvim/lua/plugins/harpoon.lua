return {
  "ThePrimeagen/harpoon",
  branch = "harpoon2",
  dependencies = { "nvim-lua/plenary.nvim" },
  config = function()
    local harpoon = require("harpoon")

    -- The default select() only moves the cursor to the saved row/col when the
    -- buffer had to be freshly loaded; if you're already on that buffer it's a
    -- no-op positionally. Always jump to the saved position instead.
    harpoon:setup({
      default = {
        select = function(list_item, list, options)
          if list_item == nil then
            return
          end
          options = options or {}

          local bufnr = vim.fn.bufnr(list_item.value)
          if bufnr == -1 then
            bufnr = vim.fn.bufadd(list_item.value)
          end
          if not vim.api.nvim_buf_is_loaded(bufnr) then
            vim.fn.bufload(bufnr)
            vim.api.nvim_set_option_value("buflisted", true, { buf = bufnr })
          end

          if options.vsplit then
            vim.cmd("vsplit")
          elseif options.split then
            vim.cmd("split")
          elseif options.tabedit then
            vim.cmd("tabedit")
          end

          vim.api.nvim_set_current_buf(bufnr)

          local lines = vim.api.nvim_buf_line_count(bufnr)
          local row = math.min(list_item.context.row or 1, lines)
          local row_text = vim.api.nvim_buf_get_lines(bufnr, row - 1, row, false)[1] or ""
          local col = math.min(list_item.context.col or 0, #row_text)

          vim.api.nvim_win_set_cursor(0, { row, col })
        end,
      },
    })

    -- Fully take over m<key> / `<key>: m<key> pins the current file to that
    -- slot, `<key> jumps to it. Replaces Vim's native marks entirely.
    local slots = "abcdefghijklmnopqrstuvwxyz0123456789"
    for i = 1, #slots do
      local key = slots:sub(i, i)

      vim.keymap.set("n", "m" .. key, function()
        harpoon:list():replace_at(i)
      end, { desc = "Harpoon: mark slot " .. key })

      vim.keymap.set("n", "`" .. key, function()
        harpoon:list():select(i)
      end, { desc = "Harpoon: jump to slot " .. key })
    end

    vim.keymap.set("n", "<leader>m", function()
      harpoon.ui:toggle_quick_menu(harpoon:list())
    end, { desc = "Harpoon: quick menu" })
  end,
}
