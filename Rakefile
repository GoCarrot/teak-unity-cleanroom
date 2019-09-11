# frozen_string_literal: true

require 'awesome_print'
require 'rake/clean'
require 'shellwords'
require 'mustache'
require 'httparty'
require 'terminal-notifier'
require 'tmpdir'
CLEAN.include '**/.DS_Store'

#
# Extend Rake to have current_task
#
require 'rake'
module Rake
  #
  # Extend Application
  #
  class Application
    attr_accessor :current_task
  end
  #
  # Extend Task
  #
  class Task
    alias old_execute execute
    def execute(args = nil)
      Rake.application.current_task = self
      old_execute(args)
    end
  end
end

desc 'Build Unity package'
task :default

UNITY_COMPILERS = %i[gmsc smcs smcs mcs csc].freeze
UNITY_HOME = ENV.fetch('UNITY_HOME', Dir.glob('/Applications/Unity*').first)
RVM_VARS = %w[GEM_HOME IRBRC MY_RUBY_HOME GEM_PATH].freeze
PROJECT_PATH = Rake.application.original_dir
BUILD_TYPE = ENV.fetch('BUILD_TYPE', 'dev')
TARGET_API = ENV.fetch('TARGET_API', 28)
TEAK_CREDENTIALS = {
  'dev' => {
    package_name: 'io.teak.app.unity.dev',
    teak_app_id: '613659812345256',
    teak_api_key: '41ff00cfd4cb85702e265aa3d5ab7858',
    teak_gcm_sender_id: '12126273415',
    teak_firebase_app_id: '1:12126273415:android:102329156b15bf0c',
    teak_short_url_domain: 'teak-dev.playw.it',
    signing_key: 'io.teak.app.unity.dev.upload.keystore'
  },
  'prod' => {
    package_name: 'io.teak.app.unity.prod',
    teak_app_id: '1136371193060244',
    teak_api_key: '1f3850f794b9093864a0778009744d03',
    teak_gcm_sender_id: '12126273415',
    teak_firebase_app_id: '1:12126273415:android:102329156b15bf0c',
    teak_short_url_domain: 'teak-prod.playw.it',
    signing_key: 'io.teak.app.unity.prod.keystore'
  }
}.freeze
PACKAGE_NAME = TEAK_CREDENTIALS[BUILD_TYPE][:package_name]
SIGNING_KEY = TEAK_CREDENTIALS[BUILD_TYPE][:signing_key]

KMS_KEY = `aws kms decrypt --ciphertext-blob fileb://kms/store_encryption_key.key --output text --query Plaintext | base64 --decode`.freeze
CIRCLE_TOKEN = ENV.fetch('CIRCLE_TOKEN') { `openssl enc -md MD5 -d -aes-256-cbc -in kms/encrypted_circle_ci_key.data -k #{KMS_KEY}` }
FB_UPLOAD_TOKEN = ENV.fetch('FB_UPLOAD_TOKEN') { `openssl enc -md MD5 -d -aes-256-cbc -in kms/encrypted_fb_upload_token.data -k #{KMS_KEY}` }

FORCE_CIRCLE_BUILD_ON_FETCH = ENV.fetch('FORCE_CIRCLE_BUILD_ON_FETCH', false)

def unity?
  File.exist? "#{UNITY_HOME}/Unity.app/Contents/MacOS/Unity"
end

def ci?
  ENV.fetch('CI', false).to_s == 'true'
end

def use_prime31?
  ENV.fetch('USE_PRIME31', false).to_s == 'true'
end

def use_unityiap?
  ENV.fetch('USE_UNITY_IAP', true).to_s == 'true'
end

def android_il2cpp?
  ENV.fetch('USE_IL2CPP_ON_ANDROID', false).to_s == 'true'
end

#
# Template parameters
#
def template_parameters
  teak_version = File.read(File.join(PROJECT_PATH, 'Assets', 'Teak', 'TeakVersion.cs')).match(/return "(.*)"/).captures[0]
  TEAK_CREDENTIALS[BUILD_TYPE].merge(
    app_name: "#{BUILD_TYPE.capitalize} #{teak_version}",
    target_api: TARGET_API
  )
end

#
# Notify when finished
#
at_exit do
  if ci?
    Rake::Task['unity_license:release'].invoke unless Rake.application.current_task.name.start_with?('unity_license')
  else
    success = $ERROR_INFO.nil?
    TerminalNotifier.notify(
      Rake.application.top_level_tasks.join(', '),
      title: 'Teak Unity Cleanroom',
      subtitle: success ? 'Succeeded' : 'Failed',
      sound: success ? 'Submarine' : 'Funk'
    )
  end
end

#
# Helper methods
#
def unity(*args, quit: true, nographics: true)
  args.push('-serial', ENV['UNITY_SERIAL'], '-username', ENV['UNITY_EMAIL'], '-password', ENV['UNITY_PASSWORD']) if ci?

  escaped_args = args.map { |arg| Shellwords.escape(arg) }.join(' ')
  log_file = "#{PROJECT_PATH}/unity.#{Rake.application.current_task.name.sub(':', '-')}.log"
  begin
    sh "#{UNITY_HOME}/Unity.app/Contents/MacOS/Unity -logFile #{log_file}#{quit ? ' -quit' : ''}#{nographics ? ' -nographics' : ''} -batchmode -projectPath #{PROJECT_PATH} #{escaped_args}", verbose: false
  rescue RuntimeError => _e
    hax_parse_log(log_file)
    abort "Unity errors in #{log_file}\n\t#{"subl://open?url=file://#{log_file}".pale}"
  end
end

def fastlane(*args, env: {})
  escaped_args = args.map { |arg| Shellwords.escape(arg) }.join(' ')
  sh "#{env.map { |k, v| "#{k}='#{v}'" }.join(' ')} bundle exec fastlane #{escaped_args}", verbose: false
end

def with_defined(defines)
  UNITY_COMPILERS.each do |compiler|
    File.open("Assets/#{compiler}.rsp", 'w') do |f|
      defines.each do |define|
        f.puts "-define:#{define}"
      end
    end
  end

  yield

  File.delete(*Dir['Assets/*.rsp*'])
end

def without_teak_available
  with_defined(['TEAK_NOT_AVAILABLE']) do
    yield
  end
end

def with_kms_decrypt(file, &block)
  current_task = Rake.application.current_task
  Rake::Task['kms:decrypt'].invoke(file)
  Rake.application.current_task = current_task
  yield file
  File.delete file
end

def print_build_msg(platform, args = nil)
  build_msg = <<~BUILD_MSG
    #{'-' * 80}
    Building #{PACKAGE_NAME} - #{platform} - #{BUILD_TYPE.capitalize}
    Teak SDK: #{`cat TEAK_VERSION`}#{"\n" + args.map { |k, v| "#{k}: #{v}" }.join(', ') if args}
    #{'-' * 80}
  BUILD_MSG
  puts build_msg.cyan
end

#
# Tasks
#
task :clean do
  sh 'git clean -fdx', verbose: false unless ci?

  xcode_artifacts = File.expand_path('~/Library/Developer/Xcode/')
  FileUtils.rm_rf Dir[File.join(xcode_artifacts, 'DerivedData', 'Unity-iPhone-*')]
  FileUtils.rm_rf Dir[File.join(xcode_artifacts, 'Archives', '**', 'teak-unity-cleanroom*')]

  Dir[File.join(xcode_artifacts, '**', '*')].select { |d| File.directory? d }
                                            .select { |d| (Dir.entries(d) - %w[. ..]).empty? }
                                            .each   { |d| Dir.rmdir d }
end

task :warnings_as_errors do
  UNITY_COMPILERS.each do |compiler|
    File.open("Assets/#{compiler}.rsp", 'w') do |f|
      f.puts '-warnaserror+'
    end
  end
end

namespace :unity_license do
  task :acquire do
    return unless ci?

    without_teak_available do
      unity '-executeMethod', 'BuildPlayer.CheckLicense', PACKAGE_NAME, nographics: false
    end
  end

  task :release do
    return unless ci?

    without_teak_available do
      begin
        sh "#{UNITY_HOME}/Unity.app/Contents/MacOS/Unity -batchmode -quit -returnlicense", verbose: false
      rescue StandardError
        nil
      end
      puts 'Released Unity license...'
    end
  end
end

namespace :prime31 do
  task :import do
    `openssl enc -md MD5 -d -aes-256-cbc -in kms/encrypted_prime31_plugin.data -out Prime31_IAP.unitypackage -k #{KMS_KEY}`

    without_teak_available do
      unity '-importPackage', 'Prime31_IAP.unitypackage'
    end

    File.delete('Prime31_IAP.unitypackage')
  end

  task :encrypt, [:path] do |_, args|
    prime31_path = args[:path] || '../IAPAndroid_3.9.unitypackage'
    `openssl enc -md MD5 -aes-256-cbc -in #{prime31_path} -out kms/encrypted_prime31_plugin.data -k #{KMS_KEY}`
  end
end

namespace :unity_iap do
  task :import do
    without_teak_available do
      unity '-importPackage', 'Assets/Plugins/UnityPurchasing/UnityIAP.unitypackage'
      FileUtils.remove_dir('Assets/Plugins/UnityPurchasing/script/Demo')
      File.delete('Assets/Plugins/UnityPurchasing/script/Demo.meta')
      File.delete(*Dir['Assets/Plugins/UnityPurchasing/script/IAP*'])
      File.delete(*Dir['Assets/Plugins/UnityPurchasing/Editor/IAPButtonEditor*'])
      File.delete(*Dir['Assets/Plugins/UnityPurchasing/script/PurchasingCheck*'])
      File.delete(*Dir['Assets/Plugins/UnityPurchasing/script/CodelessIAPStoreListener*'])

      # Assets/Plugins/UnityPurchasing/Editor/UnityIAPInstaller.cs(496,53):
      #   warning CS0618: `UnityEditor.PlayerSettings.cloudProjectId' is obsolete:
      #  `cloudProjectId is deprecated, use CloudProjectSettings.projectId instead'
      if UNITY_HOME.include? '2018'
        files_to_fix = ['Assets/Plugins/UnityPurchasing/Editor/UnityIAPInstaller.cs']

        files_to_fix.each do |file_name|
          fixed_text = File.read(file_name).gsub(/PlayerSettings\.cloudProjectId/, 'CloudProjectSettings.projectId')
          File.open(file_name, 'w') { |file| file.puts fixed_text }
        end
      end

      unity '-importPackage', 'Assets/Plugins/UnityPurchasing/UnityChannel.unitypackage'
      # File.delete(*Dir['Assets/Plugins/UnityPurchasing/Editor*'])
    end
  end
end

namespace :facebook do
  task :import do
    facebook_sdk_version = ENV.fetch('FACEBOOK_SDK_VERSION', '7.17.2')
    zip_name = if facebook_sdk_version
                 "facebook-unity-sdk-#{facebook_sdk_version}"
               else
                 'FacebookSDK-current'
               end
    url = "https://origincache.facebook.com/developers/resources/?id=#{zip_name}.zip"
    begin
      tmpdir = Dir.mktmpdir
      sdk_name = nil
      cd tmpdir, verbose: false do
        sh "curl #{url} -L -o FacebookUnitySDK.zip", verbose: false
        sh 'unzip FacebookUnitySDK.zip && rm FacebookUnitySDK.zip', verbose: false
        sdk_name = `ls | head -n 1`.strip
      end

      without_teak_available do
        unity '-importPackage', "#{tmpdir}/#{sdk_name}/FacebookSDK/#{sdk_name}.unitypackage"
      end
    ensure
      FileUtils.remove_dir('Assets/FacebookSDK/Examples')
      File.delete('Assets/FacebookSDK/Examples.meta')
      case facebook_sdk_version
      when '7.9.4'
        File.delete(*Dir['Assets/FacebookSDK/Plugins/Android/libs/support-v4-*'])
        File.delete(*Dir['Assets/FacebookSDK/Plugins/Android/libs/support-annotations-*'])
      end
      sh 'git clean -fdx Assets/PlayServicesResolver/Editor/', verbose: false
      sh 'git checkout -- Assets/PlayServicesResolver/Editor/*', verbose: false

      FileUtils.remove_entry tmpdir
    end
  end
end

namespace :package do
  task download: [:clean] do
    fastlane 'sdk'
  end

  task copy: [:clean] do
    fastlane 'sdk', env: { FL_TEAK_SDK_SOURCE: "#{PROJECT_PATH}/../teak-unity/" }
  end

  task build: [:clean] do
    Rake::Task['package:build:ios'].invoke(true)
    Rake::Task['package:build:android'].invoke(true)
    Rake::Task['package:build:unity'].invoke
  end

  namespace :build do
    task :ios, [:skip_unity?] => %i[clean] do |_, args|
      cd "#{PROJECT_PATH}/../teak-ios/", verbose: false do
        sh "./compile"
      end

      Rake::Task['package:build:unity'].invoke unless args[:skip_unity?]
    end

    task :android, [:skip_unity?] => %i[clean] do |_, args|
      cd "#{PROJECT_PATH}/../teak-android/", verbose: false do
        sh "./compile"
      end

      Rake::Task['package:build:unity'].invoke unless args[:skip_unity?]
    end

    task :unity do
      cd "#{PROJECT_PATH}/../teak-unity/", verbose: false do
        sh "BUILD_LOCAL=true NOTIFY=false rake"
      end

      Rake::Task['package:copy'].invoke
    end
  end

  task :import do
    without_teak_available do
      unity '-importPackage', 'Teak.unitypackage'
    end

    Rake::Task['prime31:import'].invoke if use_prime31?
    Rake::Task['unity_iap:import'].invoke if use_unityiap?
    Rake::Task['facebook:import'].invoke
  end
end

namespace :config do
  task all: %i[id settings apple_team_id]

  task :id do
    unity '-executeMethod', 'BuildPlayer.SetBundleId', PACKAGE_NAME
  end

  task :apple_team_id do
    unity '-executeMethod', 'BuildPlayer.SetAppleTeamId', '7FLZTACJ82' # TODO: Pull from Fastlane
  end

  task :settings do
    template = File.read(File.join(PROJECT_PATH, 'Templates', 'TeakSettings.asset.template'))
    File.write(File.join(PROJECT_PATH, 'Assets', 'Resources', 'TeakSettings.asset'), Mustache.render(template, template_parameters))

    mkdir_p File.join(PROJECT_PATH, 'Assets', 'FacebookSDK', 'SDK', 'Resources')
    template = File.read(File.join(PROJECT_PATH, 'Templates', 'FacebookSettings.asset.template'))
    File.write(File.join(PROJECT_PATH, 'Assets', 'FacebookSDK', 'SDK', 'Resources', 'FacebookSettings.asset'), Mustache.render(template, template_parameters))
  end
end

namespace :android do
  task :dependencies do
    unity '-buildTarget', 'Android', '-executeMethod', 'BuildPlayer.ResolveDependencies', quit: false, nographics: false
  end
end

namespace :build do
  task :android, [:amazon?] => %i[android:dependencies warnings_as_errors] do |_, args|
    FileUtils.rm_f('teak-unity-cleanroom.apk')

    template = File.read(File.join(PROJECT_PATH, 'Templates', 'AndroidManifest.xml.template'))
    File.write(File.join(PROJECT_PATH, 'Assets', 'Plugins', 'Android', 'AndroidManifest.xml'), Mustache.render(template, template_parameters))

    additional_args = ['--debug']
    build_amazon = args[:amazon?] ? args[:amazon?].to_s == 'true' : false
    build_il2cpp = android_il2cpp? || use_prime31?

    additional_args.concat(['--define', 'AMAZON']) if build_amazon
    additional_args.concat(['--define', 'USE_PRIME31']) if use_prime31?
    additional_args.concat(['--il2cpp']) if build_il2cpp

    print_build_msg 'Android', Store: build_amazon ? 'Amazon' : 'Google Play', IL2Cpp: build_il2cpp

    # This appeared when using Facebook SDK 7.17.2
    # When the file is deleted, it appears again during the build process
    # Writing an empty JAR file suppresses it
    if File.exist? 'Assets/Plugins/Android/com.google.zxing.core-3.3.3.jar'
      File.delete 'Assets/Plugins/Android/com.google.zxing.core-3.3.3.jar'
      Dir.mktmpdir do |dir|
        sh "jar -cf Assets/Plugins/Android/com.google.zxing.core-3.3.3.jar -C #{dir}/ ."
      end
    end

    with_kms_decrypt SIGNING_KEY do
      unity '-buildTarget', 'Android', '-executeMethod', 'BuildPlayer.Android', '--api', TARGET_API, '--keystore', File.join(PROJECT_PATH, SIGNING_KEY), *additional_args
    end
  end

  task :amazon do
    Rake::Task['build:android'].invoke('true')
  end

  task ios: ['ios:all']

  task webgl: [:warnings_as_errors] do
    print_build_msg 'WebGL'

    begin
      tmpdir = Dir.mktmpdir
      FileUtils.mv 'Assets/Plugins/UnityPurchasing', "#{tmpdir}/UnityPurchasing", force: true
      FileUtils.mv 'Assets/Plugins/UnityChannel', "#{tmpdir}/UnityChannel", force: true

      additional_args = ['--debug']
      unity '-buildTarget', 'WebGL', '-executeMethod', 'BuildPlayer.WebGL', *additional_args

      template = File.read(File.join(PROJECT_PATH, 'Templates', 'index.html.template'))
      FileUtils.mkdir_p(File.join(PROJECT_PATH, 'WebGLBuild'))
      File.write(File.join(PROJECT_PATH, 'WebGLBuild', 'index.html'), Mustache.render(template, template_parameters))
      sh '(cd WebGLBuild/; zip -r ../teak-unity-cleanroom.zip .)'
    ensure
      FileUtils.mv "#{tmpdir}/UnityPurchasing", 'Assets/Plugins/UnityPurchasing', force: true
      FileUtils.mv "#{tmpdir}/UnityChannel", 'Assets/Plugins/UnityChannel', force: true
      FileUtils.remove_entry tmpdir
    end
  end
end

namespace :ios do
  task all: %i[build fastlane]

  task :fastlane_match do
    fastlane 'match', 'development' if ci?
  end

  task build: [:warnings_as_errors] do
    print_build_msg 'iOS'

    FileUtils.rm_f('teak-unity-cleanroom.ipa')
    FileUtils.rm_f('teak-unity-cleanroom.app.dSYM.zip')

    additional_args = ['--debug']

    unity '-buildTarget', 'iOS', '-executeMethod', 'BuildPlayer.iOS', *additional_args
  end

  task fastlane: [:fastlane_match] do
    fastlane 'dev'
  end

  task :ci do
    fastlane 'ci'
  end
end

namespace :deploy do
  task :ios do
    sh 'aws s3 cp teak-unity-cleanroom.ipa s3://teak-build-artifacts/unity-cleanroom/teak-unity-cleanroom-`cat TEAK_VERSION`.ipa --acl public-read'
  end

  task :android do
    sh 'aws s3 cp teak-unity-cleanroom.apk s3://teak-build-artifacts/unity-cleanroom/teak-unity-cleanroom-`cat TEAK_VERSION`.apk --acl public-read'
  end

  task :webgl do
    sh "curl -X POST https://graph-video.facebook.com/#{TEAK_CREDENTIALS[BUILD_TYPE][:teak_app_id]}/assets -F 'access_token=#{FB_UPLOAD_TOKEN}' -F 'type=UNITY_WEBGL' -F 'asset=@./teak-unity-cleanroom.zip' -F 'comment=#{`cat TEAK_VERSION`}'"
  end

  task :google_play do
    with_kms_decrypt 'supply.key.json' do
      # fastlane 'supply', '--apk', 'teak-unity-cleanroom.apk', '--json_key', 'supply.key.json', '--track', 'internal', '--package_name', PACKAGE_NAME
      # fastlane 'google_play_track_version_codes', '--json_key', 'supply.key.json', '--track', 'internal', '--package_name', PACKAGE_NAME
      fastlane 'android', 'deploy'
    end
  end
end

namespace :install do
  task :ios do
    ipa_path = 'teak-unity-cleanroom.ipa'

    begin
      sh "ideviceinstaller --uninstall #{PACKAGE_NAME}"
    rescue StandardError
      nil
    end
    # https://github.com/libimobiledevice/libimobiledevice/issues/510#issuecomment-347175312
    sh "ideviceinstaller --install #{ipa_path}"
  end

  task :android, [:store] do |_, args|
    apk_path = 'teak-unity-cleanroom.apk'
    installer_package = args[:store] || 'com.android.vending'
    android_destination = '/data/local/tmp/teak-unity-cleanroom.apk'

    devicelist = `AndroidResources/devicelist`.split(',').collect(&:chomp)
    devicelist.each do |device|
      adb = ->(*lambda_args) { sh "adb -s #{device} #{lambda_args.join(' ')}" }

      begin
        adb.call "uninstall #{PACKAGE_NAME}"
      rescue StandardError
        nil
      end
      adb.call "push #{apk_path} #{android_destination}"
      adb.call "shell pm install -i #{installer_package} -r #{android_destination}"
      sleep 1
      adb.call "shell rm #{android_destination}"
      adb.call "shell am start -n #{PACKAGE_NAME}/com.unity3d.player.UnityPlayerActivity"
    end
  end

  task :google_play do
    Rake::Task['install:android'].invoke('com.android.vending')
  end

  task :amazon do
    Rake::Task['install:android'].invoke('com.amazon.venezia')
  end

  task :webgl do
    sh 'ruby -run -e httpd WebGlBuild -p 8000 &'
    sh 'open http://localhost:8000'
    sh 'fg'
  end
end

namespace :test do
  task :ios do
    # osascript -e 'tell application "Messages" to send "https://teak-dev.playw.it/h/i-0sMjn-F" to buddy "teak.devices@gmail.com"'
  end
end

namespace :kms do
  task :encrypt, [:path] do |_, args|
    raise "Missing keystore to encrypt. 'rake kms:encrypt[/path/to/keystore]'" unless args[:path]

    raise "Could not find file: '#{args[:path]}'" unless File.exist?(args[:path])

    `openssl enc -md MD5 -aes-256-cbc -in #{args[:path]} -out kms/#{File.basename(args[:path])}.data -k #{KMS_KEY}`
  end

  task :decrypt, [:encrypted_file] do |_, args|
    raise "Missing keystore to encrypt. 'rake kms:decrypt[file.to.decrypt]'" unless args[:encrypted_file]

    raise "Could not find file: 'kms/#{args[:encrypted_file]}.data'" unless File.exist?("kms/#{args[:encrypted_file]}.data")

    `openssl enc -md MD5 -d -aes-256-cbc -in kms/#{args[:encrypted_file]}.data -out #{args[:encrypted_file]} -k #{KMS_KEY}`
  end
end

task :hax_test do
  hax_parse_log 'unity.config-id.log'
end

def hax_parse_log(logfile)
  ret = false
  state = :ready
  command_failed = Struct.new(:desc, :command, :stderr).new
  File.foreach(logfile).with_index do |line, _line_num|
    if state == :ready
      if (matches = line.match(/^CommandInvokationFailure:(.*)$/))
        command_failed.desc = matches.captures[0].strip
        state = :command_invokation_failure_start
      elsif /^(-*)CompilerOutput:-stderr(-*)$/.match(line)
        state = :compile_error
      end
    elsif state == :command_invokation_failure_start
      command_failed.command = line.strip
      state = :command_invokation_failure_looking_for_stderr
    elsif state == :command_invokation_failure_looking_for_stderr
      state = :command_invokation_failure_stderr if /^stderr\[$/ =~ line
    elsif state == :command_invokation_failure_stderr
      if /^\]$/ =~ line
        state = :ready
        puts "⚠️  #{command_failed.desc}\n#{command_failed.stderr.join("\n")}\n\t#{command_failed.command.pale}"
        ret = true
      else
        command_failed.stderr ||= []
        command_failed.stderr << line.strip
      end
    elsif state == :compile_error
      if (matches = line.match(/^(.*)error CS([0-9]+): (.*)$/))
        file_line_col, _errno, description = matches.captures
        flc_matches = file_line_col.match(/^(.*)\(([0-9]+),([0-9]+)\): $/)
        file, line, col = flc_matches.captures if flc_matches
        puts "⚠️  #{description}#{" \n\t#{file} (#{line}, #{col})\n\tsubl://open?url=file://#{File.join(PROJECT_PATH, file)}&line=#{line}&column=#{col}".pale unless file_line_col.empty?}"
        ret = true
      end
    end

    # Reset compile error state
    state = :ready if /^(-*)EndCompilerOutput(-*)$/ =~ line
  end
  ret
end
