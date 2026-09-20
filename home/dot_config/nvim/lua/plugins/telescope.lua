return {
  "nvim-telescope/telescope.nvim",
  dependencies = { "nvim-lua/plenary.nvim" },
  config = function()
    require("telescope").setup({})

    local builtin = require("telescope.builtin")

    vim.keymap.set("n", "<leader>ff", function()
      builtin.find_files({
        cwd = vim.fn.getcwd(),
        find_command = { "rg", "--files", "--color", "never", "--glob", "!**/mow-*/**", "--glob", "!**/node_modules/**" },
        previewer = false,
        layout_config = { width = 0.95 },
      })
    end, {})
    vim.keymap.set("n", "<leader>fg", function()
      builtin.live_grep({
        cwd = vim.fn.getcwd(),
        glob_pattern = { "!*.po", "!*.pot", "!**/mow-*/**", "!**/node_modules/**" },
        additional_args = function()
          return { "-i" }
        end,
      })
    end, {})
    vim.keymap.set("n", "<leader>fb", builtin.buffers, {})
    vim.keymap.set("n", "<leader>fh", builtin.help_tags, {})
  end,
}
