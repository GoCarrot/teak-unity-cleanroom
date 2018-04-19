require "xcodeproj"
require "fileutils"
require "tmpdir"

TEAK_EXTENSIONS = [
  ["TeakNotificationService", ["MobileCoreServices", "UserNotifications", "UIKit", "SystemConfiguration"]],
  ["TeakNotificationContent", ["UserNotifications", "UserNotificationsUI", "AVFoundation", "UIKit", "ImageIO", "CoreGraphics"]]
]

module Fastlane
  module Actions
    class TeakExtensionsAction < Action
      def self.run(params)
        FastlaneCore::PrintTable.print_values(config: params, title: "Summary for Teak Extensions")
        teak_extensions_project_path = params[:teak_extensions_project_path]
        teak_extensions_source = params[:teak_extensions_source]
        teak_extensions_source_path = params[:teak_extensions_source_path]
        teak_extensions_branch = params[:teak_extensions_branch]

        # Open Xcode project
        xcode_proj = Xcodeproj::Project.open(teak_extensions_project_path)

        temp_path = nil
        begin
          # Clone Teak Extensions if needed
          if teak_extensions_source.start_with?("git@") || teak_extensions_source.start_with?("http")
            repo_name = teak_extensions_source.split("/").last.split(".").first
            checkout_param = teak_extensions_branch

            temp_path = Dir.mktmpdir("fl_teak_extensions_clone")
            clone_folder = File.join(temp_path, repo_name)

            branch_option = "--branch #{teak_extensions_branch}" if teak_extensions_branch != 'HEAD'

            UI.message("Cloning git repo for Teak Extensions (#{teak_extensions_source})...")
            Actions.sh("GIT_TERMINAL_PROMPT=0 git clone '#{teak_extensions_source}' '#{clone_folder}' --depth 1 -n #{branch_option}")

            # TODO: Versions?

            Actions.sh("cd '#{clone_folder}' && git checkout #{checkout_param} '#{teak_extensions_source_path}'")

            # Reassign teak_extensions_source to be the temp directory
            teak_extensions_source = clone_folder
          end

          # Expand path
          teak_extensions_source = File.join(File.expand_path(teak_extensions_source), teak_extensions_source_path)

          # Make sure teak_extensions_source exists
          UI.user_error!("Teak Extensions not found in: #{teak_extensions_source}") if !File.exist?(teak_extensions_source)

          # TODO: Ensure the files we need are located in teak_extensions_source
          Actions.sh("cd '#{teak_extensions_source}' && ls")

          # Destination path for copied files
          extension_destination_path = File.dirname(teak_extensions_project_path)

          # Copy the files, and add to Xcode project
          TEAK_EXTENSIONS.each do |service, deps|
            target_path = File.join(extension_destination_path, service)
            FileUtils.mkdir_p(target_path)

            # Find or create PBXGroup
            product_group = xcode_proj[service] || xcode_proj.new_group(service, service)

            # Get or create target
            target = xcode_proj.native_targets.detect { |e| e.name == service} ||
              xcode_proj.new_target(:app_extension, service, :ios, nil, xcode_proj.products_group, :objc)

            # Add target dependencies
            deps.each do |framework|
              file_ref = xcode_proj.frameworks_group.new_reference("System/Library/Frameworks/#{framework}.framework")
              file_ref.name = "#{framework}.framework"
              file_ref.source_tree = 'SDKROOT'
              target.frameworks_build_phase.add_file_reference(file_ref, true)
            end

            # Add dependency on libTeak.a
            teak_framework_ref = xcode_proj.frameworks_group.new_reference("libTeak.a")
            teak_framework_ref.name = "libTeak.a"
            teak_framework_ref.source_tree = 'SOURCE_ROOT'
            target.frameworks_build_phase.add_file_reference(teak_framework_ref, true)

            # Copy files
            Dir.glob(File.expand_path("#{service}/**/*", teak_extensions_source)).map(&File.method(:realpath)).each do |file|
              target_file = File.join(target_path, File.basename(file))
              FileUtils.rm_f(target_file)
              FileUtils.cp(file, target_file)

              # Find or add file to Xcode project
              file_ref = product_group[File.basename(file)] || product_group.new_reference(File.basename(file))

              # Add *.m files to build phase
              if File.extname(file) == ".m" then
                target.source_build_phase.add_file_reference(file_ref, true)
              end
            end

            # Add Resources build phase
            target.resources_build_phase

            # Assign build configurations
            target.build_configurations.each do |config|
              build_settings = xcode_proj.native_targets.detect { |e| e.name == xcode_proj.root_object.name }.build_settings(config.name)
              next if not build_settings
              config.build_settings = {
                :ARCHS => "arm64", # armv7 and armv7s do not support Notification Content Extensions
                :IPHONEOS_DEPLOYMENT_TARGET => 10.0,
                :DEVELOPMENT_TEAM => build_settings['DEVELOPMENT_TEAM'],
                :LIBRARY_SEARCH_PATHS => [
                    "$(SRCROOT)/Libraries/Teak/Plugins/iOS" # Unity path
                ],
                :INFOPLIST_FILE => "#{service}/Info.plist",
                :LD_RUNPATH_SEARCH_PATHS => "$(inherited) @executable_path/Frameworks @executable_path/../../Frameworks",
                :PRODUCT_BUNDLE_IDENTIFIER => "$(CFBundleIdentifier).#{service}",
                :PRODUCT_NAME => "$(TARGET_NAME)",
                :SKIP_INSTALL => :YES,
                :TARGETED_DEVICE_FAMILY => "1,2",
                :VALID_ARCHS => "arm64"
              }
            end

            # Add to native targets
            xcode_proj.native_targets.each do |native_target|
              next if native_target.to_s != xcode_proj.root_object.name

              native_target.add_dependency(target)

              copy_phase = native_target.build_phases.detect { |e| e.respond_to?(:name) && e.name == "Embed Teak App Extensions" } || native_target.new_copy_files_build_phase("Embed Teak App Extensions")
              copy_phase.dst_subfolder_spec = '13'
              copy_phase.add_file_reference(target.product_reference, true)
            end
          end
        ensure
          # Remove temporary directory (from cloning repo) if it exists
          FileUtils.remove_entry_secure(temp_path) if !temp_path.nil?
        end

        # Done, save it
        xcode_proj.save
        UI.message("Successfully saved project: #{xcode_proj.root_object.name}")
      end

      def self.description
        'Add App Extension'
      end

      def self.available_options
        [
          FastlaneCore::ConfigItem.new(key: :teak_extensions_project_path,
                                       env_name: "FL_TEAK_EXTENSIONS_PROJECT_PATH",
                                       description: "Xcode project path"),
          FastlaneCore::ConfigItem.new(key: :teak_extensions_source,
                                       env_name: "FL_TEAK_EXTENSIONS_SOURCE",
                                       description: "Path to a local checkout of the `teak-ios` repository, or git URL",
                                       default_value: "https://github.com/GoCarrot/teak-ios.git",
                                       optional: true),
          FastlaneCore::ConfigItem.new(key: :teak_extensions_source_path,
                                       env_name: "FL_TEAK_EXTENSIONS_SOURCE_PATH",
                                       description: "If `teak_extensions_source` is a git URL, the path to the extensions inside that repository",
                                       default_value: "TeakExtensions/",
                                       optional: true),
          FastlaneCore::ConfigItem.new(key: :teak_extensions_branch,
                                       env_name: "FL_TEAK_EXTENSIONS_SOURCE_BRANCH",
                                       description: "If `teak_extensions_source` is a git URL, the branch to use in that repository",
                                       default_value: "HEAD",
                                       optional: true)
          
        ]
      end

      def self.output
        [
        ]
      end

      def self.authors
        ["Pat Wilson"]
      end

      def self.is_supported?(platform)
        [:ios].include?(platform)
      end
    end
  end
end
