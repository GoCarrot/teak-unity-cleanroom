require "xcodeproj"
require "cfpropertylist"
require "fileutils"
module Fastlane
  module Actions
    class UpdateEntitlementsAction < Action
      def self.run(params)
        FastlaneCore::PrintTable.print_values(config: params, title: "Summary for Update Entitlements")
        entitlements_project_path = params[:entitlements_project_path]
        entitlements_project_configuration = params[:entitlements_project_configuration]
        entitlements_project_scheme = params[:entitlements_project_scheme]

        push_enabled = params[:push_enabled]
        push_ios = params[:push_ios]
        push_development = params[:push_development]
        iap_enabled = params[:iap_enabled]
        icloud_enabled = params[:icloud_enabled]
        keychain_enabled = params[:keychain_enabled]

        workspace_path = File.dirname(File.expand_path(entitlements_project_path))
        project = Xcodeproj::Project.open(entitlements_project_path)
        attributes = {}
        capabilities = {}
        entitlements_data = {}
        entitlements_references = []

        # INFO: Verify target
        entitlements_target = nil
        project.targets.each do |target|
          if entitlements_project_scheme == target.name
            entitlements_target = target
            break
          end
        end

        if entitlements_target == nil
          UI.user_error!("Xcode target not found, #{entitlements_project_scheme}")
        end

        # INFO: Verify configuration
        entitlements_configuration = nil
        project.build_configurations.each do |config|
          puts "POSSIBLE CONFIG: #{config.name}"
          if entitlements_project_configuration == config.name
            entitlements_configuration = config
            break
          end
        end

        if entitlements_configuration == nil
          UI.user_error!("Xcode configuration not found, #{entitlements_configuration}")
        end

        entitlements_product_name = entitlements_target.build_settings(entitlements_configuration.name)["PRODUCT_NAME"]
        entitlements_full_path = workspace_path + "/" + entitlements_target.name + "/" + entitlements_product_name + ".entitlements"
        entitlements_local_path = entitlements_target.name + "/" + entitlements_product_name + ".entitlements"

        if push_enabled
          capabilities["com.apple.Push"] = {"enabled" => push_enabled}
          push_platform = push_ios ? "aps-environment" : "com.apple.developer.aps-environment"
          push_environment = push_development ? "development" : "production"
          entitlements_data[push_platform] = push_environment
        end

        if iap_enabled
          capabilities["com.apple.InAppPurchase"] = {"enabled" => iap_enabled}
        end

        if icloud_enabled
          capabilities["com.apple.iCloud"] = {"enabled" => icloud_enabled}
          icloud_container_key = "com.apple.developer.icloud-container-identifiers"
          icloud_container_value = []
          icloud_kvstore_key = "com.apple.developer.ubiquity-kvstore-identifier"
          icloud_kvstore_value = "$(TeamIdentifierPrefix)$(CFBundleIdentifier)"
          entitlements_data[icloud_container_key] = icloud_container_value
          entitlements_data[icloud_kvstore_key] = icloud_kvstore_value
        end

        if keychain_enabled
          capabilities["com.apple.Keychain"] = {"enabled" => keychain_enabled}
          keychain_groups_key = "keychain-access-groups"
          keychain_groups_value = ["$(AppIdentifierPrefix)com.kerosene.dawnoftanks"]
          entitlements_data[keychain_groups_key] = keychain_groups_value
        end

        # TODO: Modify future capabilities here

        entitlements_plist = CFPropertyList::List.new
        entitlements_plist.value = CFPropertyList.guess(entitlements_data)
        entitlements_plist.formatted = true
        entitlements_plist.save(entitlements_full_path, CFPropertyList::List::FORMAT_XML)
        UI.message("Created entitlements file: #{entitlements_full_path}")

        project.new_file(entitlements_local_path)
        UI.message("Added entitlements file under the alias: #{entitlements_local_path}")

        attributes[entitlements_target.uuid] = {"SystemCapabilities" => capabilities}
        UI.message("Modified capabilities and references to scheme: #{entitlements_target.name}")

        project.build_configurations.each do |config|
            config.build_settings.store("CODE_SIGN_ENTITLEMENTS", entitlements_local_path)
        end
        UI.message("Modified entitlements for configurations")

        project.root_object.attributes["TargetAttributes"] = attributes
        UI.message("Modified target attributes")

        project.save
        UI.message("Successfully saved project: #{project.root_object.name}")
      end

      def self.description
        'Update Entitlements'
      end

      def self.available_options
        [
          FastlaneCore::ConfigItem.new(key: :entitlements_project_path,
                                       env_name: "FL_ENTITLEMENTS_PROJECT_PATH",
                                       description: "Xcode project path"),
          FastlaneCore::ConfigItem.new(key: :entitlements_project_scheme,
                                       env_name: "FL_ENTITLEMENTS_PROJECT_SCHEME",
                                       description: "Xcode project scheme to apply entitlements",
                                       default_value: "Unity-iPhone",
                                       optional: true),
          FastlaneCore::ConfigItem.new(key: :entitlements_project_configuration,
                                       env_name: "FL_ENTITLEMENTS_PROJECT_CONFIGURATION",
                                       description: "Xcode project configuration to apply entitlements",
                                       default_value: "Release",
                                       optional: true),
          FastlaneCore::ConfigItem.new(key: :push_enabled,
                                       env_name: "FL_PUSH_ENABLED",
                                       description: "Enable the Push Notification Entitlement",
                                       default_value: false,
                                       is_string: false,
                                       optional: true),
          FastlaneCore::ConfigItem.new(key: :push_ios,
                                       env_name: "FL_PUSH_IOS",
                                       description: "Set the platform 'ios/mac'",
                                       default_value: true,
                                       is_string: false,
                                       optional: true),
          FastlaneCore::ConfigItem.new(key: :push_development,
                                       env_name: "FL_PUSH_DEVELOPMENT",
                                       description: "Set the environment 'development/production'",
                                       default_value: true,
                                       is_string: false,
                                       optional: true),
          FastlaneCore::ConfigItem.new(key: :iap_enabled,
                                       env_name: "FL_IAP_ENABLED",
                                       description: "Enable the In-App Purchase Entitlement",
                                       default_value: false,
                                       is_string: false,
                                       optional: true),
          FastlaneCore::ConfigItem.new(key: :icloud_enabled,
                                       env_name: "FL_ICLOUD_ENABLED",
                                       description: "Enable the iCloud Purchase Entitlement",
                                       default_value: false,
                                       is_string: false,
                                       optional: true),
          FastlaneCore::ConfigItem.new(key: :keychain_enabled,
                                       env_name: "FL_KEYCHAIN_ENABLED",
                                       description: "Enable the Keychain Purchase Entitlement",
                                       default_value: false,
                                       is_string: false,
                                       optional: true)
        ]
      end

      def self.output
        [
        ]
      end

      def self.authors
        ["s.garcia39"]
      end

      def self.is_supported?(platform)
        [:ios].include?(platform)
      end
    end
  end
end
