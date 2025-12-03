local skinPedestal = {}

skinPedestal.name = "UltimateMadelineCeleste/SkinPedestal"
skinPedestal.depth = 0

skinPedestal.placements = {
    {
        name = "normal",
        data = {
            pedestalIndex = 0
        }
    }
}

-- Use Madeline's sprite for the preview
skinPedestal.texture = "characters/player/sitDown00"
skinPedestal.justification = {0.5, 1.0}

-- Field information for the editor
skinPedestal.fieldInformation = {
    pedestalIndex = {
        fieldType = "integer",
        minimumValue = 0
    }
}

return skinPedestal

