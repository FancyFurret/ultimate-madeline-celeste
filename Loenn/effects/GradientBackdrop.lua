local gradientBackdrop = {}

gradientBackdrop.name = "UltimateMadelineCeleste/GradientBackdrop"

gradientBackdrop.canBackground = true
gradientBackdrop.canForeground = true

gradientBackdrop.defaultData = {
    texture = "bgs/UMC/gradient",
    startPos = 0,
    endPos = 180,
    direction = "Vertical",
    colorHex = "000000",
    colorOpacity = 1,
    middleMode = false
}

gradientBackdrop.placements = {
    {
        name = "Gradient Backdrop",
        data = {
            texture = "bgs/UMC/gradient",
            startPos = 0,
            endPos = 180,
            direction = "Vertical",
            colorHex = "000000",
            colorOpacity = 1,
            middleMode = false
        }
    }
}

gradientBackdrop.fieldInformation = {
    texture = {
        fieldType = "string"
    },
    startPos = {
        fieldType = "number"
    },
    endPos = {
        fieldType = "number"
    },
    direction = {
        fieldType = "string",
        options = {
            "Vertical",
            "Horizontal"
        },
        editable = false
    },
    colorHex = {
        fieldType = "color"
    },
    colorOpacity = {
        fieldType = "number",
        minimumValue = 0,
        maximumValue = 1
    },
    middleMode = {
        fieldType = "boolean"
    }
}

gradientBackdrop.fieldOrder = {
    "texture",
    "startPos",
    "endPos",
    "direction",
    "colorHex",
    "colorOpacity",
    "middleMode"
}

return gradientBackdrop
