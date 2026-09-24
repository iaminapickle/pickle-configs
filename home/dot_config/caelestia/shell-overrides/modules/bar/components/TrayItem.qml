pragma ComponentBehavior: Bound

import QtQuick
import Quickshell.Services.SystemTray
import Caelestia.Config
import qs.components.effects
import qs.services
import qs.utils

MouseArea {
    id: root

    required property SystemTrayItem modelData
    required property int index
    required property var bar

    readonly property string popoutName: `traymenu${index}`
    readonly property bool hasPopout: modelData.hasMenu && Config.bar.popouts.tray

    // This MouseArea sits above the shell-wide interaction layer, so it must open the
    // popout itself instead of letting the click fall through to Bar.checkPopout()
    function togglePopout(): void {
        const popouts = bar.popouts;

        if (popouts.hasCurrent && popouts.currentName === popoutName) {
            popouts.hasCurrent = false;
            return;
        }

        popouts.currentName = popoutName;
        popouts.currentCenter = Qt.binding(() => root.mapToItem(root.bar, 0, root.implicitHeight / 2).y);
        popouts.hasCurrent = true;
    }

    acceptedButtons: Qt.LeftButton | Qt.RightButton
    implicitWidth: Tokens.font.body.small.pointSize * 2
    implicitHeight: Tokens.font.body.small.pointSize * 2

    onClicked: event => {
        if (event.button === Qt.LeftButton) {
            if (hasPopout)
                togglePopout();
            else
                modelData.activate();
        } else
            modelData.secondaryActivate();
    }

    ColouredIcon {
        id: icon

        anchors.fill: parent
        source: Icons.getTrayIcon(root.modelData.id, root.modelData.icon)
        colour: Colours.palette.m3secondary
        layer.enabled: Config.bar.tray.recolour
    }
}
