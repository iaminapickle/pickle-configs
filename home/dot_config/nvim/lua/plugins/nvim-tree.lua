return {
  "nvim-tree/nvim-tree.lua",
  dependencies = { "nvim-tree/nvim-web-devicons" },
  config = function()
    local api = require("nvim-tree.api")

    -- Custom window picker: same full-bar highlight as nvim-tree's default,
    -- but a bracketed, centered letter instead of nvim-tree's bare centered letter.
    local function custom_window_picker()
      local view = require("nvim-tree.view")
      local tree_winid = view.get_winnr()

      local chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890"
      local exclude_filetype = { "notify", "qf", "diff", "fugitive", "fugitiveblame" }
      local exclude_buftype = { "nofile", "terminal", "help" }

      local selectable = {}
      for _, id in ipairs(vim.api.nvim_tabpage_list_wins(0)) do
        if id ~= tree_winid then
          local buf = vim.api.nvim_win_get_buf(id)
          local ft = vim.api.nvim_get_option_value("filetype", { buf = buf })
          local bt = vim.api.nvim_get_option_value("buftype", { buf = buf })
          local cfg = vim.api.nvim_win_get_config(id)
          if
            not vim.tbl_contains(exclude_filetype, ft)
            and not vim.tbl_contains(exclude_buftype, bt)
            and cfg.focusable
            and not cfg.hide
            and not cfg.external
          then
            table.insert(selectable, id)
          end
        end
      end

      if #selectable == 0 then
        return -1
      end
      if #selectable == 1 then
        return selectable[1]
      end

      local laststatus = vim.o.laststatus
      vim.o.laststatus = 2

      local saved_stl, saved_hl = {}, {}
      local win_map = {}
      for i, id in ipairs(selectable) do
        local char = chars:sub(i, i)
        win_map[char] = id
        saved_stl[id] = vim.api.nvim_get_option_value("statusline", { win = id })
        saved_hl[id] = vim.api.nvim_get_option_value("winhl", { win = id })
        vim.api.nvim_set_option_value("statusline", "%=[ " .. char .. " ]%=", { win = id })
        vim.api.nvim_set_option_value("winhl", "StatusLine:NvimTreeWindowPicker,StatusLineNC:NvimTreeWindowPicker", { win = id })
      end

      vim.cmd("redraw")
      if vim.opt.cmdheight._value ~= 0 then
        print("Pick window: ")
      end

      local ok, c = pcall(vim.fn.getchar)
      while ok and type(c) ~= "number" do
        ok, c = pcall(vim.fn.getchar)
      end
      local resp = (ok and vim.fn.nr2char(c) or ""):upper()

      if vim.opt.cmdheight._value ~= 0 then
        vim.cmd("normal! :")
      end

      for id, stl in pairs(saved_stl) do
        if vim.api.nvim_win_is_valid(id) then
          vim.api.nvim_set_option_value("statusline", stl, { win = id })
          vim.api.nvim_set_option_value("winhl", saved_hl[id], { win = id })
        end
      end
      vim.o.laststatus = laststatus

      return win_map[resp]
    end

    require("nvim-tree").setup({
      hijack_netrw = true,
      sync_root_with_cwd = true,
      update_focused_file = { enable = true },
      view = { width = { min = 30, max = -1 } },
      actions = {
        open_file = {
          window_picker = {
            picker = custom_window_picker,
          },
        },
      },
      on_attach = function(bufnr)
        api.config.mappings.default_on_attach(bufnr)
        -- Drop nvim-tree's own K/L so the global K=start-of-line / L=10-down remaps apply here too
        vim.keymap.del("n", "K", { buffer = bufnr })
        vim.keymap.del("n", "L", { buffer = bufnr })
      end,
    })
    vim.keymap.set("n", "<leader>e", "<cmd>NvimTreeToggle<CR>", {})

    -- Muted, low-brightness tag instead of nvim-tree's bright default
    local function fix_window_picker_hl()
      vim.api.nvim_set_hl(0, "NvimTreeWindowPicker", { fg = "#ebdbb2", bg = "#665c54", bold = true })
    end
    fix_window_picker_hl()
    vim.api.nvim_create_autocmd("ColorScheme", { callback = fix_window_picker_hl })
  end,
}
