return {
  "lewis6991/gitsigns.nvim",
  config = function()
    require("gitsigns").setup({
      current_line_blame = true,
      current_line_blame_opts = {
        delay = 10,
        virt_text_pos = "eol",
      },
    })

    local gs = require("gitsigns")
    local hunk_mode = false

    -- Navigate to a hunk and show the diff INLINE in the real buffer (keeps
    -- surrounding context + the current line visible), instead of the floating
    -- popup. gitsigns clears the inline preview on CursorMoved via a `once`
    -- autocmd, and navigating moves the cursor, so we defer the render slightly
    -- so it lands AFTER that clear fires -- otherwise it disappears instantly.
    local function preview_inline(dir)
      gs.nav_hunk(dir)
      vim.defer_fn(function()
        -- Guard against a pending render firing after hunk mode was turned off
        -- (e.g. <Esc> pressed right after n).
        if hunk_mode then
          gs.preview_hunk_inline()
        end
      end, 20)
    end

    -- The inline preview lives in this namespace; clearing it dismisses the
    -- overlay immediately (gitsigns otherwise only clears it on cursor move).
    local preview_ns = vim.api.nvim_create_namespace("gitsigns_preview_inline")

    local function exit_hunk_mode()
      hunk_mode = false
      vim.api.nvim_buf_clear_namespace(0, preview_ns, 0, -1)
    end

    vim.keymap.set("n", "<leader>h", function()
      hunk_mode = not hunk_mode
      if hunk_mode then
        preview_inline("next")
      end
    end, { desc = "Toggle git hunk navigation mode (inline preview)" })

    vim.keymap.set("n", "n", function()
      if hunk_mode then
        preview_inline("next")
      else
        vim.cmd("normal! n")
      end
    end, { desc = "Next search match / git hunk" })

    vim.keymap.set("n", "N", function()
      if hunk_mode then
        preview_inline("prev")
      else
        vim.cmd("normal! N")
      end
    end, { desc = "Previous search match / git hunk" })

    -- <Esc> exits hunk mode (and clears the inline overlay) in addition to its
    -- usual :nohlsearch. This supersedes the <Esc> map in config/keymaps.lua,
    -- which only clears search highlight.
    vim.keymap.set("n", "<Esc>", function()
      exit_hunk_mode()
      vim.cmd("nohlsearch")
    end, { desc = "Clear search highlight + exit git hunk mode", silent = true })

    vim.api.nvim_create_autocmd({ "InsertEnter", "BufLeave" }, {
      callback = function()
        hunk_mode = false
      end,
    })
  end,
}
