local goalFlag = {}

goalFlag.name = "UltimateMadelineCeleste/GoalFlag"
goalFlag.depth = -10500

goalFlag.placements = {
    {
        name = "normal",
        data = {}
    }
}

function goalFlag.sprite(room, entity)
    local sprites = {}
    
    local x = entity.x or 0
    local y = entity.y or 0
    local poleHeight = 32
    local flagWidth = 18
    local flagHeight = 12
    
    local rectangle = require("structs.rectangle")
    local drawableRectangle = require("structs.drawable_rectangle")
    
    local poleColor = {0.2, 0.2, 0.2, 1.0}
    local flagRed = {0.86, 0.16, 0.16, 1.0}
    
    -- Draw pole
    local poleRect = rectangle.create(x - 1, y - poleHeight, 2, poleHeight)
    table.insert(sprites, drawableRectangle.fromRectangle("fill", poleRect, poleColor))
    
    -- Draw triangular pennant (simplified for editor)
    local flagStartX = x + 1
    local flagStartY = y - poleHeight + 2 + flagHeight / 2
    
    -- Draw triangle as a series of columns getting shorter
    for col = 0, flagWidth - 1 do
        local heightFactor = 1 - (col / flagWidth)
        local colHeight = math.max(1, math.floor(flagHeight * heightFactor))
        local colY = flagStartY - colHeight / 2
        
        local colRect = rectangle.create(flagStartX + col, colY, 1, colHeight)
        table.insert(sprites, drawableRectangle.fromRectangle("fill", colRect, flagRed))
    end
    
    return sprites
end

-- Selection rectangle
function goalFlag.selection(room, entity)
    return require("utils").rectangle(entity.x - 4, entity.y - 32, 24, 32)
end

return goalFlag

