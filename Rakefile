require "rake/clean"
require "shellwords"
require "mustache"
CLEAN.include "**/.DS_Store"

desc "Build Unity package"
task :default

UNITY_HOME = ENV.fetch('UNITY_HOME', '/Applications/Unity')
RVM_VARS = %w(GEM_HOME IRBRC MY_RUBY_HOME GEM_PATH)
PROJECT_PATH = Rake.application.original_dir
BUILD_TYPE = ENV.fetch('BUILD_TYPE', 'dev')
TARGET_API = ENV.fetch('TARGET_API', 25)
TEAK_CREDENTIALS = {
  'dev' => {
    package_name: 'io.teak.app.unity.dev',
    teak_app_id: '613659812345256',
    teak_api_key: '41ff00cfd4cb85702e265aa3d5ab7858',
    teak_gcm_sender_id: '944348058057',
    teak_short_url_domain: 'teak-dev.playw.it'
  },
  'prod' => {
    package_name: 'io.teak.app.unity.prod',
    teak_app_id: '1136371193060244',
    teak_api_key: '1f3850f794b9093864a0778009744d03',
    teak_gcm_sender_id: '944348058057',
    teak_short_url_domain: 'teak-prod.playw.it'
  }
}
PACKAGE_NAME = TEAK_CREDENTIALS[BUILD_TYPE][:package_name]
TEAK_SDK_VERSION = ENV.fetch('TEAK_SDK_VERSION', nil) ? "-#{ENV.fetch('TEAK_SDK_VERSION')}" : ""

#
# Template parameters
#
def template_parameters
  teak_version = File.read(File.join(PROJECT_PATH, 'Assets', 'Teak', 'TeakVersion.cs')).match(/return "(.*)"/).captures[0]
  TEAK_CREDENTIALS[BUILD_TYPE].merge({
    app_name: "#{BUILD_TYPE.capitalize} #{teak_version}",
    target_api: TARGET_API
  })
end

#
# Play a sound after finished
#
at_exit do
  sh "afplay /System/Library/Sounds/Submarine.aiff"
end

#
# Helper methods
#
def xcodebuild(*args)
  escaped_args = args.map { |arg| Shellwords.escape(arg) }.join(' ')
  sh "xcodebuild #{escaped_args} | xcpretty"
end

def unity(*args, quit: true, nographics: true)
  escaped_args = args.map { |arg| Shellwords.escape(arg) }.join(' ')
  sh "#{UNITY_HOME}/Unity.app/Contents/MacOS/Unity -logFile #{PROJECT_PATH}/unity.log#{quit ? ' -quit' : ''}#{nographics ? ' -nographics' : ''} -batchmode -projectPath #{PROJECT_PATH} #{escaped_args}"
end

def unity?
  File.exist? "#{UNITY_HOME}/Unity.app/Contents/MacOS/Unity"
end

#
# Tasks
#
task :clean do
  sh "git clean -fdx"
end

namespace :package do
  task download: [:clean] do
    sh "curl -o Teak.unitypackage https://s3.amazonaws.com/teak-build-artifacts/unity/Teak#{TEAK_SDK_VERSION}.unitypackage"
  end

  task copy: [:clean] do
    cp '../teak-unity/Teak.unitypackage', 'Teak.unitypackage'
  end

  task :import do
    File.open('Assets/smcs.rsp', 'w') do |f|
      f.puts "-define:TEAK_NOT_AVAILABLE"
    end
    File.open('Assets/mcs.rsp', 'w') do |f|
      f.puts "-define:TEAK_NOT_AVAILABLE"
    end

    unity "-importPackage", "Teak.unitypackage"

    File.delete(*Dir.glob('Assets/smcs.rsp*'))
    File.delete(*Dir.glob('Assets/mcs.rsp*'))
  end
end

namespace :config do
  task all: [:id, :settings]

  task :id do
    unity "-executeMethod", "BuildPlayer.SetBundleId", PACKAGE_NAME
  end

  task :settings do
    template = File.read(File.join(PROJECT_PATH, 'Templates', 'TeakSettings.asset.template'))
    File.write(File.join(PROJECT_PATH, 'Assets', 'Resources', 'TeakSettings.asset'), Mustache.render(template, template_parameters))
  end
end

namespace :build do
  task :dependencies do
    unity "-buildTarget", "Android", "-executeMethod", "BuildPlayer.ResolveDependencies", quit: false, nographics: false
  end

  task android: [:dependencies] do
    FileUtils.rm_f('teak-unity-cleanroom.apk')

    template = File.read(File.join(PROJECT_PATH, 'Templates', 'AndroidManifest.xml.template'))
    File.write(File.join(PROJECT_PATH, 'Assets', 'Plugins', 'Android', 'AndroidManifest.xml'), Mustache.render(template, template_parameters))

    unity "-buildTarget", "Android", "-executeMethod", "BuildPlayer.Android", TARGET_API
  end

  task ios: ['ios:all']

  task :webgl do
    unity "-executeMethod", "BuildPlayer.WebGL"
  end
end

namespace :ios do
  task all: [:build, :postprocess, :fastlane]

  task :build do
    FileUtils.rm_f('teak-unity-cleanroom.ipa')
    FileUtils.rm_f('teak-unity-cleanroom.app.dSYM.zip')
    unity "-buildTarget", "iOS", "-executeMethod", "BuildPlayer.iOS"
  end

  task :postprocess do
    template = File.read(File.join(PROJECT_PATH, 'Templates', 'Unity-iPhone.entitlements.template'))
    File.write(File.join(PROJECT_PATH, 'iOSResources', 'Unity-iPhone', 'Unity-iPhone.entitlements'), Mustache.render(template, template_parameters))

    cp 'iOSResources/Unity-iPhone/Unity-iPhone.entitlements', 'Unity-iPhone/Unity-iPhone/Unity-iPhone.entitlements'
    sh "ruby iOSResources/AddEntitlements.rb Unity-iPhone"
  end

  task :fastlane do
    sh 'bundle exec fastlane dev'
  end
end

namespace :install do
  task :ios do
    # # https://github.com/libimobiledevice/libimobiledevice/issues/510#issuecomment-347175312
    sh "ideviceinstaller -i teak-unity-cleanroom.ipa"
  end

  task :android do
    devicelist = %x[AndroidResources/devicelist].split(',').collect{ |x| x.chomp }
    devicelist.each do |device|
      adb = lambda { |*args| sh "adb -s #{device} #{args.join(' ')}" }

      begin
        adb.call "uninstall #{PACKAGE_NAME}"
      rescue
      end
      adb.call "install teak-unity-cleanroom.apk"
      adb.call "shell am start -n #{PACKAGE_NAME}/io.teak.sdk.wrapper.unity.TeakUnityPlayerActivity"
    end
  end

  task :webgl do
    sh "ruby -run -e httpd WebGlBuild -p 8000 &"
    sh "open http://localhost:8000"
    sh "fg"
  end
end
