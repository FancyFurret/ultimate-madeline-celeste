local rotatingBackdrop = {}

rotatingBackdrop.name = "UltimateMadelineCeleste/RotatingBackdrop"

rotatingBackdrop.canBackground = true
rotatingBackdrop.canForeground = true

rotatingBackdrop.defaultData = {
    texture = "bgs/UMC/stars",
    centerX = 160,
    centerY = 90,
    rotationSpeed = 5,
    scale = 1,
    opacity = 1,
    startRotation = 0
}

rotatingBackdrop.placements = {
    {
        name = "Rotating Backdrop",
        data = {
            texture = "bgs/UMC/stars",
            centerX = 160,
            centerY = 90,
            rotationSpeed = 5,
            scale = 1,
            opacity = 1,
            startRotation = 0
        }
    }
}

rotatingBackdrop.fieldInformation = {
    texture = {
        fieldType = "string"
    },
    centerX = {
        fieldType = "number"
    },
    centerY = {
        fieldType = "number"
    },
    rotationSpeed = {
        fieldType = "number"
    },
    scale = {
        fieldType = "number"
    },
    opacity = {
        fieldType = "number",
        minimumValue = 0,
        maximumValue = 1
    },
    startRotation = {
        fieldType = "number"
    }
}

rotatingBackdrop.fieldOrder = {
    "texture",
    "centerX",
    "centerY",
    "rotationSpeed",
    "scale",
    "opacity",
    "startRotation"
}

return rotatingBackdrop

