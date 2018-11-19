require 'rake/clean' # frozen_string_literal: true
require 'shellwords'
require 'mustache'
require 'httparty'
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
UNITY_HOME = ENV.fetch('UNITY_HOME', '/Applications/Unity-2017.1.0f3')
RVM_VARS = %w[GEM_HOME IRBRC MY_RUBY_HOME GEM_PATH].freeze
PROJECT_PATH = Rake.application.original_dir
BUILD_TYPE = ENV.fetch('BUILD_TYPE', 'dev')
TARGET_API = ENV.fetch('TARGET_API', 26)
TEAK_CREDENTIALS = {
  'dev' => {
    package_name: 'io.teak.app.unity.dev',
    teak_app_id: '613659812345256',
    teak_api_key: '41ff00cfd4cb85702e265aa3d5ab7858',
    teak_gcm_sender_id: '944348058057',
    teak_short_url_domain: 'teak-dev.playw.it',
    signing_key: 'io.teak.app.unity.dev.upload.keystore'
  },
  'prod' => {
    package_name: 'io.teak.app.unity.prod',
    teak_app_id: '1136371193060244',
    teak_api_key: '1f3850f794b9093864a0778009744d03',
    teak_gcm_sender_id: '944348058057',
    teak_short_url_domain: 'teak-prod.playw.it',
    signing_key: 'io.teak.app.unity.prod.keystore'
  }
}.freeze
PACKAGE_NAME = TEAK_CREDENTIALS[BUILD_TYPE][:package_name]
SIGNING_KEY = TEAK_CREDENTIALS[BUILD_TYPE][:signing_key]
TEAK_SDK_VERSION = ENV.fetch('TEAK_SDK_VERSION', nil) ? "-#{ENV.fetch('TEAK_SDK_VERSION')}" : ''

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

def add_unity_log_to_artifacts
  cp('unity.log', "#{Rake.application.current_task.name.sub(':', '-')}.unity.log") unless $!.nil?
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
# Play a sound after finished
#
at_exit do
  sh 'afplay /System/Library/Sounds/Submarine.aiff' unless ci?
  if ci?
    add_unity_log_to_artifacts
    Rake::Task['unity_license:release'].invoke unless Rake.application.current_task.name.start_with?('unity_license')
  end
end

#
# Helper methods
#
def xcodebuild(*args)
  escaped_args = args.map { |arg| Shellwords.escape(arg) }.join(' ')
  sh "xcodebuild #{escaped_args} | xcpretty"
end

def unity(*args, quit: true, nographics: true)
  args.push('-serial', ENV['UNITY_SERIAL'], '-username', ENV['UNITY_EMAIL'], '-password', ENV['UNITY_PASSWORD']) if ci?

  escaped_args = args.map { |arg| Shellwords.escape(arg) }.join(' ')
  sh "#{UNITY_HOME}/Unity.app/Contents/MacOS/Unity -logFile #{PROJECT_PATH}/unity.log#{quit ? ' -quit' : ''}#{nographics ? ' -nographics' : ''} -batchmode -projectPath #{PROJECT_PATH} #{escaped_args}", verbose: false
ensure
  add_unity_log_to_artifacts if ci?
end

def fastlane(*args, env: {})
  escaped_args = args.map { |arg| Shellwords.escape(arg) }.join(' ')
  sh "#{env.map { |k, v| "#{k}='#{v}'" }.join(' ')} bundle exec fastlane #{escaped_args}", verbose: false
end

#
# Tasks
#
task :clean do
  sh 'git clean -fdx' unless ci?
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
    # return unless ci?
    unity '-executeMethod', 'BuildPlayer.CheckLicense', PACKAGE_NAME, nographics: false
  end

  task :release do
    return unless ci?

    sh "#{UNITY_HOME}/Unity.app/Contents/MacOS/Unity -batchmode -quit -returnlicense", verbose: false rescue nil
    puts 'Released Unity license...'
  end
end

namespace :prime31 do
  task :import do
    `openssl enc -md MD5 -d -aes-256-cbc -in kms/encrypted_prime31_plugin.data -out Prime31_IAP.unitypackage -k #{KMS_KEY}`

    UNITY_COMPILERS.each do |compiler|
      File.open("Assets/#{compiler}.rsp", 'w') do |f|
        f.puts '-define:TEAK_NOT_AVAILABLE'
      end
    end

    unity '-importPackage', 'Prime31_IAP.unitypackage'

    File.delete('Prime31_IAP.unitypackage')
    File.delete(*Dir.glob('Assets/*.rsp*'))
  end

  task :encrypt, [:path] do |_, args|
    prime31_path = args[:path] || '../IAPAndroid_3.9.unitypackage'
    `openssl enc -md MD5 -aes-256-cbc -in #{prime31_path} -out kms/encrypted_prime31_plugin.data -k #{KMS_KEY}`
  end
end

namespace :unity_iap do
  task :import do
    UNITY_COMPILERS.each do |compiler|
      File.open("Assets/#{compiler}.rsp", 'w') do |f|
        f.puts '-define:TEAK_NOT_AVAILABLE'
      end
    end

    unity '-importPackage', 'Assets/Plugins/UnityPurchasing/UnityIAP.unitypackage'
    FileUtils.remove_dir('Assets/Plugins/UnityPurchasing/script/Demo')
    File.delete('Assets/Plugins/UnityPurchasing/script/Demo.meta')
    File.delete(*Dir.glob('Assets/Plugins/UnityPurchasing/script/IAP*'))
    File.delete(*Dir.glob('Assets/Plugins/UnityPurchasing/script/CodelessIAPStoreListener*'))
    File.delete(*Dir.glob('Assets/Plugins/UnityPurchasing/Editor/IAPButtonEditor*'))

    unity '-importPackage', 'Assets/Plugins/UnityPurchasing/UnityChannel.unitypackage'
    # File.delete(*Dir.glob('Assets/Plugins/UnityPurchasing/Editor*'))
  end
end

namespace :package do
  task download: [:clean] do
    fastlane 'sdk'
  end

  task copy: [:clean] do
    fastlane 'sdk', env: { FL_TEAK_SDK_SOURCE: "#{PROJECT_PATH}/../teak-unity/" }
  end

  task :import do
    UNITY_COMPILERS.each do |compiler|
      File.open("Assets/#{compiler}.rsp", 'w') do |f|
        f.puts '-define:TEAK_NOT_AVAILABLE'
      end
    end

    unity '-importPackage', 'Teak.unitypackage'

    File.delete(*Dir.glob('Assets/*.rsp*'))

    Rake::Task['prime31:import'].invoke if use_prime31?
    Rake::Task['unity_iap:import'].invoke if use_unityiap?
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

    additional_args.concat(['--define', 'AMAZON']) if build_amazon
    additional_args.concat(['--define', 'USE_PRIME31']) if use_prime31?
    additional_args.concat(['--il2cpp']) if android_il2cpp?

    Rake::Task['kms:decrypt'].invoke(SIGNING_KEY)
    unity '-buildTarget', 'Android', '-executeMethod', 'BuildPlayer.Android', '--api', TARGET_API, '--keystore', File.join(PROJECT_PATH, SIGNING_KEY), *additional_args
    File.delete(SIGNING_KEY)
  end

  task :amazon do
    Rake::Task['build:android'].invoke('true')
  end

  task ios: ['ios:all']

  task webgl: [:warnings_as_errors] do
    unity '-executeMethod', 'BuildPlayer.WebGL', '--debug'
    template = File.read(File.join(PROJECT_PATH, 'Templates', 'index.html.template'))
    FileUtils.mkdir_p(File.join(PROJECT_PATH, 'WebGLBuild'))
    File.write(File.join(PROJECT_PATH, 'WebGLBuild', 'index.html'), Mustache.render(template, template_parameters))
    sh '(cd WebGLBuild/; zip -r ../teak-unity-cleanroom.zip .)'
  end
end

namespace :ios do
  task all: %i[fastlane_match build postprocess fastlane]

  task :fastlane_match do
    fastlane 'match', 'development' if ci?
  end

  task build: [:warnings_as_errors] do
    FileUtils.rm_f('teak-unity-cleanroom.ipa')
    FileUtils.rm_f('teak-unity-cleanroom.app.dSYM.zip')

    additional_args = ['--debug']

    unity '-buildTarget', 'iOS', '-executeMethod', 'BuildPlayer.iOS', *additional_args
  end

  task :postprocess do
    template = File.read(File.join(PROJECT_PATH, 'Templates', 'Unity-iPhone.entitlements.template'))
    File.write(File.join(PROJECT_PATH, 'iOSResources', 'Unity-iPhone', 'Unity-iPhone.entitlements'), Mustache.render(template, template_parameters))

    cp File.join(PROJECT_PATH, 'iOSResources', 'Unity-iPhone', 'Unity-iPhone.entitlements'),
       File.join(PROJECT_PATH, 'Unity-iPhone', 'Unity-iPhone', 'Unity-iPhone.entitlements')
    sh 'ruby iOSResources/AddEntitlements.rb Unity-iPhone'
  end

  task :fastlane do
    fastlane 'dev'
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
    Rake::Task['kms:decrypt'].invoke('supply.key.json')
    # fastlane 'supply', '--apk', 'teak-unity-cleanroom.apk', '--json_key', 'supply.key.json', '--track', 'internal', '--package_name', PACKAGE_NAME
    # fastlane 'google_play_track_version_codes', '--json_key', 'supply.key.json', '--track', 'internal', '--package_name', PACKAGE_NAME
    fastlane 'android', 'deploy'
    File.delete('supply.key.json')
  end
end

namespace :install do
  task :ios do
    ipa_path = 'teak-unity-cleanroom.ipa'

    sh "ideviceinstaller --uninstall #{PACKAGE_NAME}" rescue nil
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

      adb.call "uninstall #{PACKAGE_NAME}" rescue nil
      adb.call "push #{apk_path} #{android_destination}"
      adb.call "shell pm install -i #{installer_package} -r #{android_destination}"
      adb.call "shell rm #{android_destination}"
      adb.call "shell am start -n #{PACKAGE_NAME}/io.teak.sdk.wrapper.unity.TeakUnityPlayerActivity"
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
