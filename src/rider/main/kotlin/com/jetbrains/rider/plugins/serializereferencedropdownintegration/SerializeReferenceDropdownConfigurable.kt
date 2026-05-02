package com.jetbrains.rider.plugins.serializereferencedropdownintegration

import com.intellij.ide.util.PropertiesComponent
import com.intellij.openapi.options.SearchableConfigurable
import com.intellij.ui.JBColor
import com.intellij.util.ui.JBUI
import java.awt.BorderLayout
import java.awt.Dimension
import java.awt.Font
import java.awt.GridBagConstraints
import java.awt.GridBagLayout
import java.nio.file.Files
import java.nio.file.Path
import java.util.Properties
import javax.swing.BorderFactory
import javax.swing.Box
import javax.swing.BoxLayout
import javax.swing.JCheckBox
import javax.swing.JComboBox
import javax.swing.JComponent
import javax.swing.JLabel
import javax.swing.JPanel

class SerializeReferenceDropdownConfigurable : SearchableConfigurable {
    private var panel: JPanel? = null
    private var showBehaviourCombo: JComboBox<ShowBehaviour>? = null
    private var autoCheckBox: JCheckBox? = null
    private var defaultApplyBox: JCheckBox? = null
    private var showWarningBox: JCheckBox? = null
    private var movedFromBehaviourCombo: JComboBox<MovedFromBehaviour>? = null
    private var showUsageCountBox: JCheckBox? = null
    private var hideZeroUsageCountBox: JCheckBox? = null
    private var autoRefreshUsageCountBox: JCheckBox? = null
    private var showUsagePreviewOnClickBox: JCheckBox? = null

    override fun getId(): String = "tools.serialize.reference.dropdown"

    override fun getDisplayName(): String = "Serialize Reference Dropdown"

    override fun createComponent(): JComponent {
        val properties = PropertiesComponent.getInstance()
        val sharedSettings = loadSharedSettings()
        val content = JPanel().apply {
            layout = BoxLayout(this, BoxLayout.Y_AXIS)
            border = JBUI.Borders.empty(4, 0, 0, 0)
        }

        val showBehaviour = JComboBox(ShowBehaviour.values()).apply {
            selectedItem = ShowBehaviour.fromStoredValue(getSetting(properties, sharedSettings, KEY_SHOW_BEHAVIOUR))
            maximumSize = Dimension(Int.MAX_VALUE, preferredSize.height)
        }
        autoCheckBox = JCheckBox("Check modified Unity asset files automatically").apply {
            isSelected = getBooleanSetting(properties, sharedSettings, KEY_AUTO_CHECK, true)
        }
        defaultApplyBox = JCheckBox("Enable \"Apply modified files\" by default after scan").apply {
            isSelected = getBooleanSetting(properties, sharedSettings, KEY_DEFAULT_APPLY, false)
        }
        showWarningBox = JCheckBox("Show warning before applying Unity asset changes").apply {
            isSelected = getBooleanSetting(properties, sharedSettings, KEY_SHOW_WARNING, true)
        }

        content.add(
            section(
                title = "Unity asset references",
                description = "Controls the extra rename step that updates Unity YAML files containing SerializeReference type names.",
            ) {
                comboRow("Rename page behaviour", showBehaviour)
                checkboxRow(autoCheckBox!!)
                checkboxRow(defaultApplyBox!!)
                checkboxRow(showWarningBox!!)
            },
        )
        content.add(Box.createVerticalStrut(JBUI.scale(12)))

        showUsageCountBox = JCheckBox("Show SerializeReference usage count in editor").apply {
            isSelected = getBooleanSetting(properties, sharedSettings, KEY_SHOW_USAGE_COUNT, true)
        }
        hideZeroUsageCountBox = JCheckBox("Hide usage count when there are no Unity asset usages").apply {
            isSelected = getBooleanSetting(properties, sharedSettings, KEY_HIDE_ZERO_USAGE_COUNT, false)
        }
        autoRefreshUsageCountBox = JCheckBox("Refresh usage count database automatically").apply {
            isSelected = getBooleanSetting(properties, sharedSettings, KEY_AUTO_REFRESH_USAGE_COUNT, false)
        }
        showUsagePreviewOnClickBox = JCheckBox("Show affected Unity asset files when clicking usage count").apply {
            isSelected = getBooleanSetting(properties, sharedSettings, KEY_SHOW_USAGE_PREVIEW_ON_CLICK, true)
        }
        content.add(
            section(
                title = "Usage Count",
                description = "Controls Code Vision hints above C# classes and the Unity asset preview opened from them.",
            ) {
                checkboxRow(showUsageCountBox!!)
                checkboxRow(hideZeroUsageCountBox!!)
                checkboxRow(autoRefreshUsageCountBox!!)
                checkboxRow(showUsagePreviewOnClickBox!!)
            },
        )
        content.add(Box.createVerticalStrut(JBUI.scale(12)))

        val movedFromBehaviour = JComboBox(MovedFromBehaviour.values()).apply {
            selectedItem = MovedFromBehaviour.fromStoredValue(getSetting(properties, sharedSettings, KEY_MOVED_FROM_BEHAVIOUR))
            maximumSize = Dimension(Int.MAX_VALUE, preferredSize.height)
        }
        content.add(
            section(
                title = "MovedFrom attribute",
                description = "Controls whether class rename should add UnityEngine.Scripting.APIUpdating.MovedFrom automatically.",
            ) {
                comboRow("MovedFrom on class rename", movedFromBehaviour)
            },
        )

        showBehaviourCombo = showBehaviour
        movedFromBehaviourCombo = movedFromBehaviour
        panel = JPanel(BorderLayout()).apply {
            border = JBUI.Borders.empty(8, 10, 0, 10)
            add(content, BorderLayout.NORTH)
        }

        return panel!!
    }

    override fun isModified(): Boolean {
        val properties = PropertiesComponent.getInstance()
        val sharedSettings = loadSharedSettings()
        return showBehaviourCombo?.selectedItem != ShowBehaviour.fromStoredValue(getSetting(properties, sharedSettings, KEY_SHOW_BEHAVIOUR)) ||
            autoCheckBox?.isSelected != getBooleanSetting(properties, sharedSettings, KEY_AUTO_CHECK, true) ||
            defaultApplyBox?.isSelected != getBooleanSetting(properties, sharedSettings, KEY_DEFAULT_APPLY, false) ||
            showWarningBox?.isSelected != getBooleanSetting(properties, sharedSettings, KEY_SHOW_WARNING, true) ||
            showUsageCountBox?.isSelected != getBooleanSetting(properties, sharedSettings, KEY_SHOW_USAGE_COUNT, true) ||
            hideZeroUsageCountBox?.isSelected != getBooleanSetting(properties, sharedSettings, KEY_HIDE_ZERO_USAGE_COUNT, false) ||
            autoRefreshUsageCountBox?.isSelected != getBooleanSetting(properties, sharedSettings, KEY_AUTO_REFRESH_USAGE_COUNT, false) ||
            showUsagePreviewOnClickBox?.isSelected != getBooleanSetting(properties, sharedSettings, KEY_SHOW_USAGE_PREVIEW_ON_CLICK, true) ||
            movedFromBehaviourCombo?.selectedItem != MovedFromBehaviour.fromStoredValue(getSetting(properties, sharedSettings, KEY_MOVED_FROM_BEHAVIOUR))
    }

    override fun apply() {
        val properties = PropertiesComponent.getInstance()
        val sharedSettings = loadSharedSettings()

        setSetting(properties, sharedSettings, KEY_SHOW_BEHAVIOUR, (showBehaviourCombo?.selectedItem as? ShowBehaviour)?.storedValue ?: ShowBehaviour.ShowAlways.storedValue)
        setSetting(properties, sharedSettings, KEY_AUTO_CHECK, (autoCheckBox?.isSelected ?: true).toString())
        setSetting(properties, sharedSettings, KEY_DEFAULT_APPLY, (defaultApplyBox?.isSelected ?: false).toString())
        setSetting(properties, sharedSettings, KEY_SHOW_WARNING, (showWarningBox?.isSelected ?: true).toString())
        setSetting(properties, sharedSettings, KEY_SHOW_USAGE_COUNT, (showUsageCountBox?.isSelected ?: true).toString())
        setSetting(properties, sharedSettings, KEY_HIDE_ZERO_USAGE_COUNT, (hideZeroUsageCountBox?.isSelected ?: false).toString())
        setSetting(properties, sharedSettings, KEY_AUTO_REFRESH_USAGE_COUNT, (autoRefreshUsageCountBox?.isSelected ?: false).toString())
        setSetting(properties, sharedSettings, KEY_SHOW_USAGE_PREVIEW_ON_CLICK, (showUsagePreviewOnClickBox?.isSelected ?: true).toString())
        setSetting(properties, sharedSettings, KEY_MOVED_FROM_BEHAVIOUR, (movedFromBehaviourCombo?.selectedItem as? MovedFromBehaviour)?.storedValue ?: MovedFromBehaviour.ShowPopup.storedValue)

        saveSharedSettings(sharedSettings)
    }

    override fun reset() {
        val properties = PropertiesComponent.getInstance()
        val sharedSettings = loadSharedSettings()
        showBehaviourCombo?.selectedItem = ShowBehaviour.fromStoredValue(getSetting(properties, sharedSettings, KEY_SHOW_BEHAVIOUR))
        autoCheckBox?.isSelected = getBooleanSetting(properties, sharedSettings, KEY_AUTO_CHECK, true)
        defaultApplyBox?.isSelected = getBooleanSetting(properties, sharedSettings, KEY_DEFAULT_APPLY, false)
        showWarningBox?.isSelected = getBooleanSetting(properties, sharedSettings, KEY_SHOW_WARNING, true)
        showUsageCountBox?.isSelected = getBooleanSetting(properties, sharedSettings, KEY_SHOW_USAGE_COUNT, true)
        hideZeroUsageCountBox?.isSelected = getBooleanSetting(properties, sharedSettings, KEY_HIDE_ZERO_USAGE_COUNT, false)
        autoRefreshUsageCountBox?.isSelected = getBooleanSetting(properties, sharedSettings, KEY_AUTO_REFRESH_USAGE_COUNT, false)
        showUsagePreviewOnClickBox?.isSelected = getBooleanSetting(properties, sharedSettings, KEY_SHOW_USAGE_PREVIEW_ON_CLICK, true)
        movedFromBehaviourCombo?.selectedItem = MovedFromBehaviour.fromStoredValue(getSetting(properties, sharedSettings, KEY_MOVED_FROM_BEHAVIOUR))
    }

    override fun disposeUIResources() {
        panel = null
        showBehaviourCombo = null
        autoCheckBox = null
        defaultApplyBox = null
        showWarningBox = null
        movedFromBehaviourCombo = null
        showUsageCountBox = null
        hideZeroUsageCountBox = null
        autoRefreshUsageCountBox = null
        showUsagePreviewOnClickBox = null
    }

    private fun section(title: String, description: String, fill: JPanel.() -> Unit): JPanel =
        JPanel(BorderLayout()).apply {
            border = BorderFactory.createCompoundBorder(
                BorderFactory.createLineBorder(JBColor.border()),
                JBUI.Borders.empty(12, 14),
            )

            val header = JPanel().apply {
                layout = BoxLayout(this, BoxLayout.Y_AXIS)
                isOpaque = false
                add(JLabel(title).apply { font = font.deriveFont(Font.BOLD, font.size2D + 1f) })
                add(Box.createVerticalStrut(JBUI.scale(3)))
                add(JLabel(description).apply { foreground = JBColor.GRAY })
            }
            add(header, BorderLayout.NORTH)

            val body = JPanel(GridBagLayout()).apply {
                border = JBUI.Borders.emptyTop(10)
                isOpaque = false
                fill()
            }
            add(body, BorderLayout.CENTER)
        }

    private fun JPanel.comboRow(labelText: String, comboBox: JComboBox<*>) {
        val row = componentCount
        add(JLabel(labelText), labelConstraints(row))
        add(comboBox, fieldConstraints(row))
    }

    private fun JPanel.checkboxRow(checkBox: JCheckBox) {
        val row = componentCount
        add(checkBox, fullRowConstraints(row))
    }

    private fun labelConstraints(row: Int): GridBagConstraints =
        GridBagConstraints().apply {
            gridx = 0
            gridy = row
            anchor = GridBagConstraints.WEST
            insets = JBUI.insets(4, 0, 4, 12)
        }

    private fun fieldConstraints(row: Int): GridBagConstraints =
        GridBagConstraints().apply {
            gridx = 1
            gridy = row
            weightx = 1.0
            fill = GridBagConstraints.HORIZONTAL
            insets = JBUI.insets(4, 0)
        }

    private fun fullRowConstraints(row: Int): GridBagConstraints =
        GridBagConstraints().apply {
            gridx = 0
            gridy = row
            gridwidth = 2
            weightx = 1.0
            fill = GridBagConstraints.HORIZONTAL
            anchor = GridBagConstraints.WEST
            insets = JBUI.insets(3, 0)
        }

    private enum class ShowBehaviour(val storedValue: String, private val presentableName: String) {
        ShowAlways("ShowAlways", "Show every time"),
        DontShow("DontShow", "Do not show");

        override fun toString(): String = presentableName

        companion object {
            fun fromStoredValue(value: String?): ShowBehaviour =
                values().firstOrNull { it.storedValue == value } ?: ShowAlways
        }
    }

    private enum class MovedFromBehaviour(val storedValue: String, private val presentableName: String) {
        ShowPopup("ShowPopup", "Ask every time"),
        AlwaysAdd("AlwaysAdd", "Always add MovedFrom"),
        NeverAdd("NeverAdd", "Never add MovedFrom");

        override fun toString(): String = presentableName

        companion object {
            fun fromStoredValue(value: String?): MovedFromBehaviour =
                values().firstOrNull { it.storedValue == value } ?: ShowPopup
        }
    }

    private companion object {
        const val KEY_SHOW_BEHAVIOUR = "serializeReferenceDropdown.modifyYamlShowBehaviour"
        const val KEY_AUTO_CHECK = "serializeReferenceDropdown.autoCheckModifiedUnityAssetFiles"
        const val KEY_DEFAULT_APPLY = "serializeReferenceDropdown.defaultApplyModifiedUnityAssetFiles"
        const val KEY_SHOW_WARNING = "serializeReferenceDropdown.showApplyModifiedUnityAssetFilesWarning"
        const val KEY_MOVED_FROM_BEHAVIOUR = "serializeReferenceDropdown.movedFromRefactoringSettings"
        const val KEY_SHOW_USAGE_COUNT = "serializeReferenceDropdown.showUsageCountCodeVision"
        const val KEY_HIDE_ZERO_USAGE_COUNT = "serializeReferenceDropdown.hideZeroUsageCountCodeVision"
        const val KEY_AUTO_REFRESH_USAGE_COUNT = "serializeReferenceDropdown.autoRefreshUsageCountDatabase"
        const val KEY_SHOW_USAGE_PREVIEW_ON_CLICK = "serializeReferenceDropdown.showUsagePreviewOnClick"

        val SHARED_SETTINGS_PATH: Path = Path.of(
            System.getProperty("user.home"),
            ".serialize-reference-dropdown-integration",
            "settings.properties",
        )

        fun getSetting(properties: PropertiesComponent, sharedSettings: Properties, key: String): String? =
            sharedSettings.getProperty(key) ?: properties.getValue(key)

        fun getBooleanSetting(properties: PropertiesComponent, sharedSettings: Properties, key: String, defaultValue: Boolean): Boolean =
            getSetting(properties, sharedSettings, key)?.let {
                it.equals("true", ignoreCase = true)
            } ?: defaultValue

        fun setSetting(properties: PropertiesComponent, sharedSettings: Properties, key: String, value: String) {
            properties.setValue(key, value)
            sharedSettings.setProperty(key, value)
        }

        fun loadSharedSettings(): Properties =
            Properties().apply {
                if (Files.exists(SHARED_SETTINGS_PATH)) {
                    Files.newInputStream(SHARED_SETTINGS_PATH).use(::load)
                }
            }

        fun saveSharedSettings(settings: Properties) {
            Files.createDirectories(SHARED_SETTINGS_PATH.parent)
            Files.newOutputStream(SHARED_SETTINGS_PATH).use {
                settings.store(it, "Serialize Reference Dropdown Integration settings")
            }
        }
    }
}
