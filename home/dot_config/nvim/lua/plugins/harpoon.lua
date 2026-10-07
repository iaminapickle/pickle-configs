return {
  "ThePrimeagen/harpoon",
  branch = "harpoon2",
  dependencies = { "nvim-lua/plenary.nvim" },
  config = function()
    local harpoon = require("harpoon")
    local Path = require("plenary.path")

    -- Live positions: each item is backed by an extmark while its buffer is
    -- loaded, so edits above it shift it like a native mark.
    local ns = vim.api.nvim_create_namespace("harpoon_marks")
    local tracked = setmetatable({}, { __mode = "k" }) -- item -> { buf, id }

    local function same_file(item, buf)
      return vim.fn.fnamemodify(item.value, ":p") == vim.api.nvim_buf_get_name(buf)
    end

    -- Copy the extmark position back into item.context.
    local function refresh(item)
      local t = tracked[item]
      if t and vim.api.nvim_buf_is_loaded(t.buf) then
        local pos = vim.api.nvim_buf_get_extmark_by_id(t.buf, ns, t.id, {})
        if pos[1] then
          item.context.row = pos[1] + 1
          item.context.col = pos[2]
        end
      end
    end

    local function untrack(item)
      local t = item and tracked[item]
      if not t then
        return
      end
      refresh(item)
      if vim.api.nvim_buf_is_valid(t.buf) then
        vim.api.nvim_buf_del_extmark(t.buf, ns, t.id)
      end
      tracked[item] = nil
    end

    local function track(item, buf)
      if tracked[item] and tracked[item].buf == buf then
        return
      end
      untrack(item)
      local row = math.max(1, math.min(item.context.row or 1, vim.api.nvim_buf_line_count(buf)))
      local text = vim.api.nvim_buf_get_lines(buf, row - 1, row, false)[1] or ""
      local col = math.min(item.context.col or 0, #text)
      tracked[item] = { buf = buf, id = vim.api.nvim_buf_set_extmark(buf, ns, row - 1, col, {}) }
    end

    harpoon:setup({
      marks = {
        -- Every slot is independent: no dedupe by file, no BufLeave overwrite.
        equals = function(a, b)
          return a == b
        end,
        BufLeave = function() end,

        create_list_item = function(config, name)
          -- From the quick menu: "path:row[:col]"
          if name then
            local path, row, col = name:match("^(.-):(%d+):?(%d*)$")
            return {
              value = path or name,
              context = { row = tonumber(row) or 1, col = tonumber(col) or 0 },
            }
          end
          local buf = vim.api.nvim_get_current_buf()
          local pos = vim.api.nvim_win_get_cursor(0)
          local item = {
            value = Path:new(vim.api.nvim_buf_get_name(buf)):make_relative(config.get_root_dir()),
            context = { row = pos[1], col = pos[2] },
          }
          track(item, buf)
          return item
        end,

        display = function(item)
          refresh(item)
          return item.value .. ":" .. item.context.row
        end,

        encode = function(item)
          refresh(item)
          return vim.json.encode({ value = item.value, context = item.context })
        end,

        select = function(item, _, options)
          if item == nil then
            return
          end
          options = options or {}

          local bufnr = vim.fn.bufadd(item.value)
          if not vim.api.nvim_buf_is_loaded(bufnr) then
            vim.fn.bufload(bufnr)
            vim.api.nvim_set_option_value("buflisted", true, { buf = bufnr })
          end

          vim.cmd("normal! m'") -- add to jumplist, like native `
          if options.vsplit then
            vim.cmd("vsplit")
          elseif options.split then
            vim.cmd("split")
          elseif options.tabedit then
            vim.cmd("tabedit")
          end

          vim.api.nvim_set_current_buf(bufnr)
          track(item, bufnr)
          refresh(item)
          vim.api.nvim_win_set_cursor(0, { item.context.row, item.context.col })
        end,
      },
    })

    local function marks()
      return harpoon:list("marks")
    end

    local function for_items_in(buf, fn)
      local list = marks()
      for i = 1, list:length() do
        local item = list:get(i)
        if item and same_file(item, buf) then
          fn(item)
        end
      end
    end

    -- Reattach on load / :e!, save position before reload or unload.
    local group = vim.api.nvim_create_augroup("HarpoonMarks", { clear = true })
    vim.api.nvim_create_autocmd("BufReadPost", {
      group = group,
      callback = function(ev)
        for_items_in(ev.buf, function(item)
          track(item, ev.buf)
        end)
      end,
    })
    vim.api.nvim_create_autocmd({ "BufReadPre", "BufUnload" }, {
      group = group,
      callback = function(ev)
        for_items_in(ev.buf, untrack)
      end,
    })

    -- m<key> sets a slot to the cursor position, `<key> jumps to it.
    local slots = "abcdefghijklmnopqrstuvwxyz0123456789"
    for i = 1, #slots do
      local key = slots:sub(i, i)

      vim.keymap.set("n", "m" .. key, function()
        if vim.api.nvim_buf_get_name(0) == "" then
          return
        end
        untrack(marks():get(i))
        marks():replace_at(i)
        harpoon:sync()
      end, { desc = "Harpoon: mark slot " .. key })

      vim.keymap.set("n", "`" .. key, function()
        marks():select(i)
      end, { desc = "Harpoon: jump to slot " .. key })
    end

    vim.keymap.set("n", "<leader>m", function()
      harpoon.ui:toggle_quick_menu(marks())
    end, { desc = "Harpoon: quick menu" })
  end,
}
