local scrollingBackdrop = {}

scrollingBackdrop.name = "UltimateMadelineCeleste/ScrollingBackdrop"

scrollingBackdrop.canBackground = true
scrollingBackdrop.canForeground = true

scrollingBackdrop.defaultData = {
    texture = "bgs/UMC/stars",
    scrollSpeedX = 10,
    yPosition = 0,
    scale = 1,
    opacity = 1
}

scrollingBackdrop.placements = {
    {
        name = "Scrolling Backdrop",
        data = {
            texture = "bgs/UMC/stars",
            scrollSpeedX = 10,
            yPosition = 0,
            scale = 1,
            opacity = 1
        }
    }
}

scrollingBackdrop.fieldInformation = {
    texture = {
        fieldType = "string"
    },
    scrollSpeedX = {
        fieldType = "number"
    },
    yPosition = {
        fieldType = "number"
    },
    scale = {
        fieldType = "number"
    },
    opacity = {
        fieldType = "number",
        minimumValue = 0,
        maximumValue = 1
    }
}

scrollingBackdrop.fieldOrder = {
    "texture",
    "scrollSpeedX",
    "yPosition",
    "scale",
    "opacity"
}

return scrollingBackdrop

