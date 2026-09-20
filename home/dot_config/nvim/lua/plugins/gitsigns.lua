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

    vim.keymap.set("n", "<leader>h", function()
      hunk_mode = not hunk_mode
      if hunk_mode then
        gs.nav_hunk("next", { preview = true })
      end
    end, { desc = "Toggle git hunk navigation mode" })

    vim.keymap.set("n", "n", function()
      if hunk_mode then
        gs.nav_hunk("next", { preview = true })
      else
        vim.cmd("normal! n")
      end
    end, { desc = "Next search match / git hunk" })

    vim.keymap.set("n", "N", function()
      if hunk_mode then
        gs.nav_hunk("prev", { preview = true })
      else
        vim.cmd("normal! N")
      end
    end, { desc = "Previous search match / git hunk" })

    vim.api.nvim_create_autocmd({ "InsertEnter", "BufLeave" }, {
      callback = function()
        hunk_mode = false
      end,
    })
  end,
}
