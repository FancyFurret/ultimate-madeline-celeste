local levelButton = {}

levelButton.name = "UltimateMadelineCeleste/LevelButton"
levelButton.depth = 9000

-- Approximately 10 tiles wide, 7 tiles tall
levelButton.placements = {
    {
        name = "normal",
        data = {
            mapSID = "",
            previewTexture = "",
            text = "",
            textColor = "FFFFFF"
        }
    }
}

-- Field information for the editor
levelButton.fieldInformation = {
    mapSID = {
        fieldType = "string"
    },
    previewTexture = {
        fieldType = "string"
    },
    text = {
        fieldType = "string",
    },
    textColor = {
        fieldType = "string",
    }
}

-- Custom rendering in Loenn
function levelButton.sprite(room, entity)
    local sprites = {}
    
    local x = entity.x or 0
    local y = entity.y or 0
    local width = 64
    local height = 48
    local buttonBaseHeight = 5
    local previewHeight = height - buttonBaseHeight
    
    -- Orange dimensions
    local orangeWidth = width - 8  -- 56px
    local orangeX = x + 4  -- centered: (width - orangeWidth) / 2 = 4
    
    -- Preview dimensions (full width - 2px)
    local previewWidth = width - 2  -- 62px
    local previewX = x + (width - previewWidth) / 2  -- centered
    
    -- Draw preview area (smaller than orange, centered, resting on gray base)
    local previewRect = require("structs.rectangle").create(previewX, y, previewWidth, previewHeight)
    table.insert(sprites, require("structs.drawable_rectangle").fromRectangle("fill", previewRect, {0.2, 0.3, 0.4, 0.8}))
    table.insert(sprites, require("structs.drawable_rectangle").fromRectangle("line", previewRect, {0.4, 0.5, 0.6, 1.0}))
    
    -- Draw orange button part (3 pixels peeking out, centered)
    local orangeY = y + height - buttonBaseHeight - 3
    local orangeRect = require("structs.rectangle").create(orangeX, orangeY, orangeWidth, 4)
    table.insert(sprites, require("structs.drawable_rectangle").fromRectangle("fill", orangeRect, {1.0, 0.5, 0.0, 1.0}))
    
    -- Draw gray button base
    local baseRect = require("structs.rectangle").create(x, y + height - buttonBaseHeight, width, buttonBaseHeight)
    table.insert(sprites, require("structs.drawable_rectangle").fromRectangle("fill", baseRect, {0.4, 0.4, 0.4, 1.0}))
    table.insert(sprites, require("structs.drawable_rectangle").fromRectangle("line", baseRect, {0.3, 0.3, 0.3, 1.0}))
    
    return sprites
end

-- Selection rectangle
function levelButton.selection(room, entity)
    return require("utils").rectangle(entity.x, entity.y, 64, 48)
end

return levelButton

