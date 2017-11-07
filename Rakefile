require "rake/clean"
require "shellwords"
CLEAN.include "**/.DS_Store"

desc "Build Unity package"
task :default

UNITY_HOME = ENV.fetch('UNITY_HOME', '/Applications/Unity')
RVM_VARS = %w(GEM_HOME IRBRC MY_RUBY_HOME GEM_PATH)
PROJECT_PATH = Rake.application.original_dir

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

def unity(*args)
  escaped_args = args.map { |arg| Shellwords.escape(arg) }.join(' ')
  sh "#{UNITY_HOME}/Unity.app/Contents/MacOS/Unity -logFile #{PROJECT_PATH}/unity.log -quit -batchmode -nographics -projectPath #{PROJECT_PATH} #{escaped_args}"
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
    sh "curl -o Teak.unitypackage https://s3.amazonaws.com/teak-build-artifacts/unity/Teak.unitypackage"
  end

  task copy: [:clean] do
    cp '../teak-unity/Teak.unitypackage', 'Teak.unitypackage'
  end

  task :import do
    File.open('Assets/smcs.rsp', 'w') do |f|
      f.puts "-define:TEAK_NOT_AVAILABLE"
    end

    unity "-importPackage", "Teak.unitypackage"

    File.delete(*Dir.glob('Assets/smcs.rsp*'))
  end
end

namespace :config do
  task :id, [:app_id] do |t, args|
    args.with_defaults(:app_id => "io.teak.sdk.sd")
    unity "-executeMethod", "BuildPlayer.SetBundleId", args[:app_id]
  end
end

namespace :build do
  task :android do
    FileUtils.rm_f('teak-unity-cleanroom.apk')
    unity "-executeMethod", "BuildPlayer.Android"
  end

  task ios: ['ios:all']

  task :webgl do
    unity "-executeMethod", "BuildPlayer.WebGL"
  end
end

namespace :ios do
  task all: [:build, :postprocess, :xcode, :export]

  task :build do
    FileUtils.rm_f('teak-unity-cleanroom.ipa')
    unity "-executeMethod", "BuildPlayer.iOS"
  end

  task :postprocess do
    cp 'iOSResources/Unity-iPhone/Unity-iPhone.entitlements', 'iOSBuild/Unity-iPhone/Unity-iPhone.entitlements'
    sh "ruby iOSResources/AddEntitlements.rb Unity-iPhone"
  end

  task :xcode do
    cd('iOSBuild') do
      xcodebuild "-project", "Unity-iPhone.xcodeproj", "-scheme", "Unity-iPhone", "-allowProvisioningUpdates", "-sdk", "iphoneos", "-configuration", "Debug", "clean", "archive", "-archivePath", "build/archive", "DEVELOPMENT_TEAM=7FLZTACJ82"
    end
  end

  task :export do
    old = {}
    RVM_VARS.each do |key|
      old[key] = ENV[key]
    end
    old['PATH'] = ENV['PATH']

    begin
      %w(GEM_HOME IRBRC MY_RUBY_HOME GEM_PATH).each do |var|
        ENV.delete(var)
      end
      ENV['PATH'] = ENV['PATH'].split(':').reject { |elem| elem =~ /\.rvm/ }.join(':')
      xcodebuild "-exportArchive", "-archivePath", "iOSBuild/build/archive.xcarchive", "-exportOptionsPlist", "iOSResources/exportOptions.plist", "-exportPath", "iOSBuild/build/", "-allowProvisioningUpdates"
    ensure
      old.each do |key, value|
        ENV[key] = value
      end
    end

    cp 'iOSBuild/build/Unity-iPhone.ipa', 'teak-unity-cleanroom.ipa'
  end
end

namespace :install do
  task :ios do
    sh "ideviceinstaller -i teak-unity-cleanroom.ipa"
  end

  task :android do
    devicelist = %x[AndroidResources/devicelist].split(',').collect{ |x| x.chomp }
    devicelist.each do |device|
      adb = lambda { |*args| sh "adb -s #{device} #{args.join(' ')}" }

      begin
        adb.call "uninstall io.teak.sdk.sd"
      rescue
      end
      adb.call "install teak-unity-cleanroom.apk"
      adb.call "shell am start -W -a android.intent.action.VIEW -d https://teakangrybots.jckpt.me/ihx_k8KPT io.teak.sdk.sd"
    end
  end

  task :webgl do
    sh "ruby -run -e httpd WebGlBuild -p 8000 &"
    sh "open http://localhost:8000"
    sh "fg"
  end
end
