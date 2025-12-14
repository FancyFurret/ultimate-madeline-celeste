local livesBalancer = {}

livesBalancer.name = "UltimateMadelineCeleste/LivesBalancer"
livesBalancer.depth = 8999
livesBalancer.placements = {
    name = "Lives Balancer",
    data = {}
}

function livesBalancer.sprite(room, entity)
    local sprites = {}
    local drawableSprite = require("structs.drawable_sprite")
    
    local x = entity.x or 0
    local y = entity.y or 0
    
    -- Billboard texture (74x81)
    local billboard = drawableSprite.fromTexture("objects/UMC/livesBalancer/billboard", entity)
    billboard:setJustification(0, 0) -- Top-left origin
    billboard:setPosition(x, y)
    table.insert(sprites, billboard)
    
    -- Buttons (attached to bottom of billboard at y + 47)
    local btnY = y + 47
    local btnWidth = 12
    local spacing = 3
    local totalWidth = btnWidth * 3 + spacing * 2
    local startX = x + (74 - totalWidth) / 2
    
    local subBtn = drawableSprite.fromTexture("objects/UMC/livesBalancer/buttonSub", entity)
    subBtn:setJustification(0, 0)
    subBtn:setPosition(startX, btnY)
    table.insert(sprites, subBtn)
    
    local addBtn = drawableSprite.fromTexture("objects/UMC/livesBalancer/buttonAdd", entity)
    addBtn:setJustification(0, 0)
    addBtn:setPosition(startX + btnWidth + spacing, btnY)
    table.insert(sprites, addBtn)
    
    local resetBtn = drawableSprite.fromTexture("objects/UMC/livesBalancer/buttonReset", entity)
    resetBtn:setJustification(0, 0)
    resetBtn:setPosition(startX + (btnWidth + spacing) * 2, btnY)
    table.insert(sprites, resetBtn)
    
    return sprites
end

function livesBalancer.selection(room, entity)
    return require("utils").rectangle(entity.x, entity.y, 74, 81)
end

return livesBalancer
